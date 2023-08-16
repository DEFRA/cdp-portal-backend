using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Tenants;

public interface IDeploymentsService
{
    Task<List<Deployment>> FindLatest(int offset = 0);
    public Task<List<Deployment>> FindLatest(string environment, int offset = 0);
    public Task<List<Deployment>> FindWhatsRunningWhere();
    public Task<List<Deployment>> FindWhatsRunningWhere(string serviceName);
    public Task<Deployment?> FindDeployment(string deploymentId);

    public Task Insert(Deployment deployment);
}

public class DeploymentsService : MongoService<Deployment>, IDeploymentsService
{
    private const string CollectionName = "deployments";

    public DeploymentsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(
        connectionFactory,
        CollectionName, loggerFactory)
    {
    }

    public async Task<List<Deployment>> FindLatest(int offset = 0)
    {
        return await Collection
            .Find(FilterDefinition<Deployment>.Empty)
            .Skip(offset)
            .Limit(200)
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt))
            .ToListAsync();
    }

    public async Task<List<Deployment>> FindLatest(string environment, int offset = 0)
    {
        return await Collection
            .Find(d => d.Environment == environment)
            .Skip(offset)
            .Limit(200)
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt))
            .ToListAsync();
    }

    public async Task<List<Deployment>> FindWhatsRunningWhere()
    {
        var pipeline = new EmptyPipelineDefinition<Deployment>()
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt))
            .Group(d => new { d.Service, d.Environment }, grp => new { Root = grp.First() })
            .Project(grp => grp.Root);

        return await Collection.Aggregate(pipeline).ToListAsync();
    }

    public async Task<List<Deployment>> FindWhatsRunningWhere(string serviceName)
    {
        var pipeline = new EmptyPipelineDefinition<Deployment>()
            .Match(d => d.Service == serviceName)
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt))
            .Group(d => new { d.Environment }, grp => new { Root = grp.First() })
            .Project(grp => grp.Root);

        return await Collection.Aggregate(pipeline).ToListAsync();
    }

    public Task<Deployment?> FindDeployment(string deploymentId)
    {
        return Collection.Find(d => d.DeploymentId == deploymentId)
            .FirstOrDefaultAsync()!;
    }

    public async Task Insert(Deployment deployment)
    {
        await Collection.InsertOneAsync(deployment);
    }

    protected override List<CreateIndexModel<Deployment>> DefineIndexes(IndexKeysDefinitionBuilder<Deployment> builder)
    {
        var indexModel = new CreateIndexModel<Deployment>(builder.Combine(builder.Ascending(r => r.Environment),
            builder.Ascending(r => r.Service)));
        var titleModel = new CreateIndexModel<Deployment>(builder.Descending(r => r.DeployedAt));
        return new List<CreateIndexModel<Deployment>> { indexModel, titleModel };
    }
}