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

    private readonly ILogger<ShutteringService> _logger = loggerFactory.CreateLogger<ShutteringService>();

    private readonly HashSet<string> _shutterV2Environments =
        (configuration.GetValue<string>("ShutterV2Environments") ?? "")
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .ToHashSet(StringComparer.OrdinalIgnoreCase);

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

                var status = ShutteringStatus(requestedState?.Shuttered, urlData.Shuttered);
                var urlType = UrlToWafUrlType(url, envConfig);
                var waf = ResolveWaf(env, url, envConfig, urlData);
                
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

    public static ShutteringStatus ShutteringStatus(bool? request, bool actual)
    {
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

    /// <summary>
    /// Resolves the WAF ACL for a URL: stored value for shutter-v2 environments, legacy heuristic otherwise.
    /// Logs a warning if a shutter-v2 environment is missing the stored value.
    /// </summary>
    private string? ResolveWaf(string env, string url, CdpTenant envConfig, TenantUrl urlData)
    {
        if (!_shutterV2Environments.Contains(env))
        {
            return LegacyUrlToWaf(url, envConfig);
        }

        if (urlData.WafWebAcl == null)
        {
            _logger.LogWarning(
                "Missing waf_web_acl for shutter-v2 environment {Environment}, url {Url}. " +
                "Platform state may not have been republished since this URL was added, or cdp-tf-waf " +
                "isn't tracking it yet.", env, url);
        }

        return urlData.WafWebAcl;
    }

    /// <summary>
    /// Computes the WAF ACL category from tenant config. Used only for environments not yet on shutter v2.
    /// See: https://github.com/DEFRA/cdp-platform-documentation/blob/main/infrastructure/shuttering.md
    /// Remove once shutter v2 has fully rolled out.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="envConfig"></param>
    /// <returns></returns>
    public static string LegacyUrlToWaf(string url, CdpTenant envConfig)
    {
        var isPublic = envConfig.TenantConfig?.Zone == "public";
        var isNginx = envConfig.Nginx?.Servers.ContainsKey(url);
        var isInternal = url.EndsWith(".defra.cloud");

        return (isPublic, isNginx, isInternal) switch
        {
            (true, true, false) => "external_public",
            (true, true, true) => "internal_public",
            (false, true, true) => "internal_protected",

            (true, false, false) => "api_public",
            (false, false, false) => "api_private",
            _ => "external_public"
        };
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