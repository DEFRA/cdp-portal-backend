using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
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
    IEntitiesService entitiesService,
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
        var shutteringRecords = await Collection.Find(s => s.ServiceName == serviceName)
            .ToListAsync(cancellationToken);
        Logger.LogInformation("Found {Count} shuttering records for service {ServiceName}", shutteringRecords.Count,
            serviceName);

        var states = new List<ShutteringUrlState>();

        foreach (var vanity in vanityUrls)
        {
            var shutteringRecord = shutteringRecords.FirstOrDefault(s =>
                s.Environment == vanity.Environment &&
                s.ServiceName == vanity.ServiceName &&
                s.Url == vanity.Url);

            var shutteringStateForVanityUrl =
                await ShutteringStateForVanityUrl(cancellationToken, shutteringRecord, vanity);
            if (shutteringStateForVanityUrl != null)
            {
                states.Add(shutteringStateForVanityUrl);
            }
        }

        return states;
    }

    private async Task<ShutteringUrlState?> ShutteringStateForVanityUrl(CancellationToken cancellationToken,
        ShutteringRecord shutteringRecord,
        VanityUrlRecord vanity)
    {
        var entity = await entitiesService.GetEntity(vanity.ServiceName, cancellationToken);
        if (entity == null)
        {
            Logger.LogWarning("No entity found for service {ServiceName}", vanity.ServiceName);
            return null;
        }

        var subType = entity.SubType;

        var waf = subType switch
        {
            SubType.Frontend when vanity.Enabled => "external_public",
            SubType.Frontend when !vanity.Enabled => "internal_public",
            SubType.Backend => "internal_protected",
            _ => throw new ArgumentOutOfRangeException()
        };

        var status = ShutteringStatus(vanity, shutteringRecord);

        var lastActionedBy = shutteringRecord?.ActionedBy;
        var lastActionedAt = shutteringRecord?.ActionedAt;

        return new ShutteringUrlState
        {
            Environment = vanity.Environment,
            ServiceName = vanity.ServiceName,
            Url = vanity.Url,
            Waf = waf,
            Internal = vanity.Enabled,
            Status = status,
            LastActionedBy = lastActionedBy,
            LastActionedAt = lastActionedAt
        };
    }

    public async Task<ShutteringUrlState?> ShutteringStateForUrl(string url, CancellationToken cancellationToken)
    {
        var vanityRecord = await vanityUrlsService.FindByUrl(url, cancellationToken);
        if (vanityRecord == null)
        {
            Logger.LogWarning("No vanity URL found for {Url}", url);
            return null;
        }

        var shutteringRecord = await Collection.Find(s =>
                s.Environment == vanityRecord.Environment &&
                s.ServiceName == vanityRecord.ServiceName &&
                s.Url == vanityRecord.Url)
            .FirstOrDefaultAsync(cancellationToken);

        return await ShutteringStateForVanityUrl(cancellationToken, shutteringRecord, vanityRecord);
    }

    public static ShutteringStatus ShutteringStatus(VanityUrlRecord vanity, ShutteringRecord? shutteringRecord)
    {
        ShutteringStatus status;
        if (vanity.Shuttered)
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
}