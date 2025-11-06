using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Models;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Shuttering;

public interface IShutteringService
{
    public Task Register(ShutteringRecord shutteringRecord, CancellationToken cancellationToken);
    Task<List<ShutteringUrlState>> ShutteringStatesForService(string name, CancellationToken cancellationToken);
    Task<ShutteringUrlState?> ShutteringStatesForService(string name, string url, CancellationToken cancellationToken);
}

public class ShutteringService(
    IMongoDbClientFactory connectionFactory,
    IEntitiesService entitiesService,
    IShutteringArchiveService shutteringArchiveService,
    ILoggerFactory loggerFactory)
    : MongoService<ShutteringRecord>(connectionFactory,
        CollectionName, loggerFactory), IShutteringService
{
    private const string CollectionName = "shutteringrecords";

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
                var waf = UrlToWaf(url, envConfig);

                output.Add(new ShutteringUrlState
                {
                    Environment = env,
                    Internal = false,
                    ServiceName = name,
                    Url = url,
                    Waf = waf,
                    LastActionedAt = requestedState?.ActionedAt,
                    LastActionedBy = requestedState?.ActionedBy,
                    Status = status
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
    /// See: https://github.com/DEFRA/cdp-platform-documentation/blob/main/infrastructure/shuttering.md
    /// This will only be needed in the short-term. Shuttering is going to be reworked so we dont need
    /// to explicitly pass the WAF as a parameter.
    /// </summary>
    /// <param name="url"></param>
    /// <param name="envConfig"></param>
    /// <returns></returns>
    public static string UrlToWaf(string url, CdpTenant envConfig)
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

    protected override List<CreateIndexModel<ShutteringRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<ShutteringRecord> builder)
    {
        var service = new CreateIndexModel<ShutteringRecord>(builder.Descending(s => s.ServiceName)
        );
        return [service];
    }
}

[Obsolete("Remove later")]
public record ShutterableUrl(
    string ServiceName,
    string Environment,
    string Url,
    bool Enabled,
    bool Shuttered,
    bool isVanity);