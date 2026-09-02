using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Grafana;

public interface IGrafanaPlaygroundService
{
    Task UpdatePlaygroundForService(GrafanaPlaygroundResources playgrounds, CancellationToken cancellationToken);

    Task<GrafanaPlaygroundResources?> FindPlaygroundsForService(string service, CancellationToken cancellationToken);
}

public class GrafanaPlaygroundService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) :
    MongoService<GrafanaPlaygroundResources>(connectionFactory, "grafanaplaygrounds", loggerFactory), IGrafanaPlaygroundService
{
    protected override List<CreateIndexModel<GrafanaPlaygroundResources>> DefineIndexes(IndexKeysDefinitionBuilder<GrafanaPlaygroundResources> builder)
    {
        var serviceIdx = builder.Ascending(g => g.Service);
        return [new CreateIndexModel<GrafanaPlaygroundResources>(serviceIdx)];
    }

    /// <summary>
    /// Updates the playground resources for a service.
    /// </summary>
    /// <param name="playgrounds"></param>
    /// <param name="cancellationToken"></param>
    public async Task UpdatePlaygroundForService(GrafanaPlaygroundResources playgrounds, CancellationToken cancellationToken)
    {
        await Collection.ReplaceOneAsync(f => f.Service == playgrounds.Service, playgrounds with { Updated = DateTime.UtcNow} ,
            new ReplaceOptions { IsUpsert = true }, cancellationToken);
    }

    /// <summary>
    /// Finds the latest playground dashboards for a service
    /// </summary>
    /// <param name="service"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<GrafanaPlaygroundResources?> FindPlaygroundsForService(string service, CancellationToken cancellationToken)
    {
        return await Collection.Find(f => f.Service == service).FirstOrDefaultAsync(cancellationToken);
    }
}
