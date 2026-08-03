using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;
using Defra.Cdp.Backend.Api.Services.Notifications;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Shuttering;

public interface IShutteringService
{
    Task Register(ShutteringRecord shutteringRecord, CancellationToken cancellationToken);
    Task<List<ShutteringUrlState>> ShutteringStatesForService(string name, CancellationToken cancellationToken);
    Task<ShutteringUrlState?> ShutteringStatesForService(string name, string url, CancellationToken cancellationToken);
}

public class ShutteringService(
    IMongoDbClientFactory connectionFactory,
    IEntitiesService entitiesService,
    IShutteringArchiveService shutteringArchiveService,
    INotificationDispatcher notificationDispatcher,
    IConfiguration configuration,
    ILoggerFactory loggerFactory)
    : MongoService<ShutteringRecord>(connectionFactory,
        CollectionName, loggerFactory), IShutteringService
{
    private const string CollectionName = "shutteringrecords";
    private const int DefaultPendingTimeoutSeconds = 1800; // 30 minutes

    private readonly ILogger<ShutteringService> _logger = loggerFactory.CreateLogger<ShutteringService>();

    private readonly TimeSpan _pendingTimeout = TimeSpan.FromSeconds(
        configuration.GetValue<int>("ShutteringPendingTimeoutSeconds", DefaultPendingTimeoutSeconds));

    public async Task Register(ShutteringRecord shutteringRecord, CancellationToken cancellationToken)
    {
        var fb = new FilterDefinitionBuilder<ShutteringRecord>();

        var filter = fb.And(
            fb.Eq(s => s.ServiceName, shutteringRecord.ServiceName),
            fb.Eq(s => s.Environment, shutteringRecord.Environment),
            fb.Eq(s => s.Url, shutteringRecord.Url)
        );

        await Collection.ReplaceOneAsync(filter, shutteringRecord, new ReplaceOptions { IsUpsert = true },
            cancellationToken);

        await shutteringArchiveService.Archive(shutteringRecord, cancellationToken);
        await notificationDispatcher.Dispatch(MapToEvent(shutteringRecord), cancellationToken);
    }

    public async Task<List<ShutteringUrlState>> ShutteringStatesForService(string name,
        CancellationToken cancellationToken)
    {
        var output = new List<ShutteringUrlState>();
        var entity = await entitiesService.GetEntity(name, cancellationToken);
        if (entity == null) return output;

        foreach (var (env, envConfig) in entity.Environments)
        {
            foreach (var (url, urlData) in envConfig.Urls)
            {
                if (urlData.Type != "vanity") continue;
                var requestedState = await Collection
                    .Find(s => s.ServiceName == name && s.Url == url && s.Environment == env)
                    .FirstOrDefaultAsync(cancellationToken);

                var isTimedOut = IsPendingTimedOut(requestedState?.ActionedAt);
                var status = ShutteringStatus(requestedState?.Shuttered, urlData.Shuttered, isTimedOut);
                var urlType = UrlToWafUrlType(url, envConfig);
                var waf = ResolveWaf(env, url, urlData);
                
                output.Add(new ShutteringUrlState
                {
                    Environment = env,
                    Internal = false,
                    ServiceName = name,
                    Url = url,
                    UrlType = urlType,
                    Waf = waf,
                    LastActionedAt = requestedState?.ActionedAt,
                    LastActionedBy = requestedState?.ActionedBy,
                    RequestedShuttered = requestedState?.Shuttered,
                    Status = status,
                    Delegated = urlData.Delegated
                });
            }
        }

        return output;
    }

    public async Task<ShutteringUrlState?> ShutteringStatesForService(string name, string url,
        CancellationToken cancellationToken)
    {
        var urls = await ShutteringStatesForService(name, cancellationToken);
        return urls.FirstOrDefault(u => u.Url.Equals(url, StringComparison.OrdinalIgnoreCase));
    }

    public static ShutteringStatus ShutteringStatus(bool? request, bool actual, bool timedOut = false)
    {
        if (timedOut && request is not null)
        {
            return actual ? Models.ShutteringStatus.Shuttered : Models.ShutteringStatus.Active;
        }

        return (request, actual) switch
        {
            (null, true) => Models.ShutteringStatus.Shuttered,
            (null, false) => Models.ShutteringStatus.Active,
            (true, true) => Models.ShutteringStatus.Shuttered,
            (true, false) => Models.ShutteringStatus.PendingShuttered,
            (false, true) => Models.ShutteringStatus.PendingActive,
            (false, false) => Models.ShutteringStatus.Active
        };
    }

    private bool IsPendingTimedOut(DateTime? actionedAt)
    {
        if (actionedAt is null)
        {
            return false;
        }

        return DateTime.UtcNow - actionedAt.Value > _pendingTimeout;
    }

    /// <summary>
    /// Resolves the WAF ACL for a URL from platform state.
    /// Logs a warning if the platform state payload is missing a value.
    /// </summary>
    private string? ResolveWaf(string env, string url, TenantUrl urlData)
    {
        if (urlData.WafWebAcl == null)
        {
            _logger.LogWarning(
                "Missing waf_web_acl in platform state for environment {Environment}, url {Url}. " +
                "Platform state may not have been republished since this URL was added.",
                env,
                url);
        }

        return urlData.WafWebAcl;
    }

    
    /// <summary>
    /// Required by the shuttering workflows.
    /// If the url is present in the nginx config then it's a vanity url, else its a WAF
    /// </summary>
    /// <param name="url"></param>
    /// <param name="envConfig"></param>
    /// <returns></returns>
    public static string UrlToWafUrlType(string url, CdpTenant envConfig)
    {
        var isNginx = envConfig.Nginx?.Servers.ContainsKey(url) ?? false;
        return isNginx ? ShutterUrlType.FrontendVanityUrl : ShutterUrlType.ApiGatewayVanityUrl;
    }

    protected override List<CreateIndexModel<ShutteringRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<ShutteringRecord> builder)
    {
        var service = new CreateIndexModel<ShutteringRecord>(builder.Descending(s => s.ServiceName)
        );
        return [service];
    }

    private static INotificationEvent MapToEvent(ShutteringRecord shutteringRecord)
    {
        if (shutteringRecord.Shuttered)
        {
            return new ShutteredEvent
            {
                Entity = shutteringRecord.ServiceName,
                Environment = shutteringRecord.Environment,
                Url = shutteringRecord.Url,
                ActionedByDisplayName = shutteringRecord.ActionedBy.DisplayName
            };
        }

        return new UnshutteredEvent
        {
            Entity = shutteringRecord.ServiceName,
            Environment = shutteringRecord.Environment,
            Url = shutteringRecord.Url,
            ActionedByDisplayName = shutteringRecord.ActionedBy.DisplayName
        };
    }
}