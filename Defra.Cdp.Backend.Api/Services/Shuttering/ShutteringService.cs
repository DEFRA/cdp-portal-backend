using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
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

        var requestedStates = await Collection
            .Find(s => s.ServiceName == name)
            .ToListAsync(cancellationToken);

        var entity = await entitiesService.GetEntity(name, cancellationToken);
        if (requestedStates == null || entity == null) return output;

        foreach (var requestedState in requestedStates)
        {
            if (!entity.Environments.TryGetValue(requestedState.Environment, out var config)) continue;
            if (!config.Urls.TryGetValue(requestedState.Url, out var tenantUrl)) continue;
            var status = ShutteringStatus(requestedState.Shuttered, tenantUrl.Shuttered);
            output.Add(new ShutteringUrlState
            {
                Environment = requestedState.Environment,
                Internal = tenantUrl.Type == "internal",
                ServiceName = name,
                Url = requestedState.Url,
                Waf = requestedState.Waf,
                LastActionedAt = requestedState.ActionedAt,
                LastActionedBy = requestedState.ActionedBy,
                Status = status
            });
        }

        return output;
    }

    public async Task<ShutteringUrlState?> ShutteringStatesForService(string name, string url,
        CancellationToken cancellationToken)
    {
        var requestedState = await Collection
            .Find(s => s.ServiceName == name && s.Url == url)
            .FirstOrDefaultAsync(cancellationToken);

        var entity = await entitiesService.GetEntity(name, cancellationToken);
        if (requestedState == null || entity == null) return null;
        if (!entity.Environments.TryGetValue(requestedState.Environment, out var config)) return null;
        if (!config.Urls.TryGetValue(url, out var tenantUrl)) return null;

        var status = ShutteringStatus(requestedState.Shuttered, tenantUrl.Shuttered);

        return new ShutteringUrlState
        {
            Environment = requestedState.Environment,
            Internal = tenantUrl.Type == "internal",
            ServiceName = name,
            Url = url,
            Waf = requestedState.Waf,
            LastActionedAt = requestedState.ActionedAt,
            LastActionedBy = requestedState.ActionedBy,
            Status = status
        };
    }

    public static ShutteringStatus ShutteringStatus(bool request, bool actual)
    {
        return (request, actual) switch
        {
            (true, true) => Models.ShutteringStatus.Shuttered,
            (true, false) => Models.ShutteringStatus.PendingShuttered,
            (false, true) => Models.ShutteringStatus.PendingActive,
            (false, false) => Models.ShutteringStatus.Active
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