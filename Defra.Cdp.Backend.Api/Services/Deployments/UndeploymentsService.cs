using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Deployments;

public interface IUndeploymentsService
{
    Task RegisterUndeployment(Undeployment undeployment, CancellationToken ct);
    Task<Undeployment?> FindUndeployment(string undeploymentId, CancellationToken ct);
}

public class UndeploymentsService : MongoService<Undeployment>, IUndeploymentsService
{
    public static readonly int DefaultPageSize = 50;
    public static readonly int DefaultPage = 1;

    public UndeploymentsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(connectionFactory, "undeployments", loggerFactory)
    {
    }

    protected override List<CreateIndexModel<Undeployment>> DefineIndexes(IndexKeysDefinitionBuilder<Undeployment> builder)
    {
        var created = new CreateIndexModel<Undeployment>(builder.Descending(d => d.Created));
        var updated = new CreateIndexModel<Undeployment>(builder.Descending(d => d.Updated));
        var cdpUndeploymentId = new CreateIndexModel<Undeployment>(builder.Descending(d => d.CdpUndeploymentId));


        return new List<CreateIndexModel<Undeployment>> { created, updated, cdpUndeploymentId };
    }

    public async Task RegisterUndeployment(Undeployment undeployment, CancellationToken ct)
    {
        await Collection.InsertOneAsync(undeployment, null, ct);
    }

    public async Task<Undeployment?> FindUndeployment(string undeploymentId, CancellationToken ct)
    {
        return await Collection.Find(d => d.CdpUndeploymentId == undeploymentId).FirstOrDefaultAsync(ct);
    }

}