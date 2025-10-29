using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Shuttering;

public interface IShutteringService
{
    public Task Register(ShutteringRecord shutteringRecord, CancellationToken cancellationToken);
    Task<List<ShutteringUrlState>> ShutteringStatesForService(string serviceName, CancellationToken cancellationToken);
    Task<ShutteringUrlState?> ShutteringStateForUrl(string url, CancellationToken cancellationToken);
}

public class ShutteringService(
    IShutteringArchiveService shutteringArchiveService,
    IMongoDbClientFactory connectionFactory,
    IVanityUrlsService vanityUrlsService,
    IApiGatewaysService apiGatewaysService,
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

    public async Task<List<ShutteringUrlState>> ShutteringStatesForService(string serviceName,
        CancellationToken cancellationToken)
    {
        var vanityUrls = await vanityUrlsService.FindService(serviceName, cancellationToken);
        Logger.LogInformation("Found {Count} vanity URLs for service {ServiceName}", vanityUrls.Count, serviceName);

        var states = new List<ShutteringUrlState>();

        foreach (var vanity in vanityUrls)
        {
            var shutteringRecord = await Collection.Find(s => s.ServiceName == serviceName
                                                              && s.Environment == vanity.Environment
                                                              && s.Url == vanity.Url)
                .FirstOrDefaultAsync(cancellationToken);

            var shutteringStateForVanityUrl = ShutteringStateForVanityUrl(vanity.ToShutterableUrl(), shutteringRecord);
            states.Add(shutteringStateForVanityUrl);
        }

        var apiGateways = await apiGatewaysService.FindService(serviceName, cancellationToken);
        Logger.LogInformation("Found {Count} api gateways for service {ServiceName}", apiGateways.Count, serviceName);

        foreach (var apiGateway in apiGateways)
        {
            var shutteringRecord = await Collection.Find(s => s.ServiceName == serviceName
                                                              && s.Environment == apiGateway.Environment
                                                              && s.Url == apiGateway.Api)
                .FirstOrDefaultAsync(cancellationToken);

            var shutteringStateForVanityUrl = ShutteringStateForVanityUrl(apiGateway.ToShutterableUrl(), shutteringRecord);
            states.Add(shutteringStateForVanityUrl);
        }

        return states;
    }

    private static ShutteringUrlState ShutteringStateForVanityUrl(ShutterableUrl shutterableUrl,
        ShutteringRecord? shutteringRecord)
    {
        var status = ShutteringStatus(shutterableUrl.Shuttered, shutteringRecord);

        var waf = shutterableUrl.isVanity switch
        {
            true => shutterableUrl.Enabled ? "external_public" : "internal_public",
            false => "api_public"
        };

        var lastActionedBy = shutteringRecord?.ActionedBy;
        var lastActionedAt = shutteringRecord?.ActionedAt;

        return new ShutteringUrlState
        {
            Environment = shutterableUrl.Environment,
            ServiceName = shutterableUrl.ServiceName,
            Url = shutterableUrl.Url,
            Waf = waf,
            Internal = shutterableUrl.Enabled,
            Status = status,
            LastActionedBy = lastActionedBy,
            LastActionedAt = lastActionedAt
        };
    }

    public async Task<ShutteringUrlState?> ShutteringStateForUrl(string url, CancellationToken cancellationToken)
    {
        var shutterableUrl = await vanityUrlsService.FindByUrl(url, cancellationToken) ??
                             await apiGatewaysService.FindByUrl(url, cancellationToken);
        if (shutterableUrl == null)
        {
            Logger.LogWarning("No vanity URL or API Gateway found for {Url}", url);
            return null;
        }

        var shutteringRecord = await Collection.Find(s =>
                s.Environment == shutterableUrl.Environment &&
                s.ServiceName == shutterableUrl.ServiceName &&
                s.Url == shutterableUrl.Url)
            .FirstOrDefaultAsync(cancellationToken);

        return ShutteringStateForVanityUrl(shutterableUrl, shutteringRecord);
    }

    public static ShutteringStatus ShutteringStatus(bool shuttered, ShutteringRecord? shutteringRecord)
    {
        ShutteringStatus status;
        if (shuttered)
        {
            if (shutteringRecord == null || shutteringRecord.Shuttered)
            {
                status = Models.ShutteringStatus.Shuttered;
            }
            else
            {
                status = Models.ShutteringStatus.PendingActive;
            }
        }
        else
        {
            status = shutteringRecord is { Shuttered: true }
                ? Models.ShutteringStatus.PendingShuttered
                : Models.ShutteringStatus.Active;
        }

        return status;
    }

    protected override List<CreateIndexModel<ShutteringRecord>> DefineIndexes(
        IndexKeysDefinitionBuilder<ShutteringRecord> builder)
    {
        var service = new CreateIndexModel<ShutteringRecord>(builder.Descending(s => s.ServiceName)
        );
        return [service];
    }
    
    public async Task<List<ShutteringUrlState>> ShutteringStatesForService2(string serviceName,
        CancellationToken cancellationToken)
    {
        
        var states = new List<ShutteringUrlState>();
        Entity? entity = null;//await entitiesService.GetEntity(serviceName, cancellationToken);
        if (entity == null)
        {
            return states;
        }
        // [BsonIgnoreExtraElements]
        // public record VanityUrlRecord(string Url, string Environment, string ServiceName, bool Enabled, bool Shuttered)
        // {

        var vanityUrls = new List<ShutterableUrl>();
        foreach (var (env, tenant) in entity.Envs)
        {
            foreach (var (url, urlConfig) in tenant.Urls)
            {
                vanityUrls.Add(new ShutterableUrl(
                    entity.Name,
                    env,
                    url,
                    urlConfig.Enabled || urlConfig.Type == "internal",
                    urlConfig.Shuttered,
                    urlConfig.Type == "vanity"
                ));
            }
        }
        
        
        Logger.LogInformation("Found {Count} vanity URLs for service {ServiceName}", vanityUrls.Count, serviceName);

        foreach (var vanity in vanityUrls)
        {
            var shutteringRecord = await Collection.Find(s => s.ServiceName == serviceName
                                                              && s.Environment == vanity.Environment
                                                              && s.Url == vanity.Url)
                .FirstOrDefaultAsync(cancellationToken);

            var shutteringStateForVanityUrl = ShutteringStateForVanityUrl(vanity, shutteringRecord);
            states.Add(shutteringStateForVanityUrl);
        }
        return states;
    }
}

public record ShutterableUrl(
    string ServiceName,
    string Environment,
    string Url,
    bool Enabled,
    bool Shuttered,
    bool isVanity);