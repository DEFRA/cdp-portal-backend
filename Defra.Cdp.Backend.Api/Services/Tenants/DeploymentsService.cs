using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Tenants;

public interface IDeploymentsService
{
    Task<List<Deployment>> FindLatest(int offset = 0);
    public Task<List<Deployment>> FindLatest(string? environment, int offset = 0);
    public Task<List<Deployment>> FindWhatsRunningWhere();
    public Task<List<Deployment>> FindWhatsRunningWhere(string serviceName);
    public Task<Deployment?> FindDeployment(string deploymentId);
    public Task<Deployment?> FindRequestedDeployment(string service, string version, string environment, DateTime deployedAt, string taskId);
    public Task<UpdateResult> LinkRequestedDeployment(ObjectId? id, Deployment deployment);

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

    public async Task<List<Deployment>> FindLatest(string? environment, int offset = 0)
    {
        if (string.IsNullOrWhiteSpace(environment)) return await FindLatest(offset);

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
        return Collection.Find(d => d.DeploymentId == deploymentId).SortBy(d => d.DeployedAt)
            .FirstOrDefaultAsync()!;
    }

    // Used to join up an incoming ECS request with requested deployment from cdp-self-service-ops
    // We look for any task that matches the name/version/env and happened up to 30 minutes before the first deployment 
    // event. 
    public async Task<Deployment?> FindRequestedDeployment(string service, string version, string environment, DateTime timestamp, string taskId)
    {
        // See if we've already matched the requested deployment via deployment id
        var requested = await Collection.Find(d => d.TaskId == taskId && d.Status == "REQUESTED").FirstOrDefaultAsync();

        if (requested != null)
        {
            return requested;
        }

        var filterBuilder = Builders<Deployment>.Filter;

        var filter = filterBuilder.And(
            filterBuilder.Eq(d => d.Service, service),
            filterBuilder.Eq(d => d.Version, version),
            filterBuilder.Eq(d => d.Environment, environment),
            filterBuilder.Eq(d => d.DeploymentId, null),
            filterBuilder.Gt(d => d.DeployedAt, timestamp.AddMinutes(-30)),
            filterBuilder.Lte(d => d.DeployedAt, timestamp)
        );

        return await Collection
            .Find(filter)
            .SortBy(d => d.DeployedAt)
            .FirstOrDefaultAsync()!;
    }

    public async Task<UpdateResult> LinkRequestedDeployment(ObjectId? id, Deployment deployment)
    {
        var update = Builders<Deployment>.Update
            .Set(d => d.TaskId, deployment.TaskId)
            .Set(d => d.DeploymentId, deployment.DeploymentId)
            .Set(d => d.DockerImage, deployment.DockerImage);
        
        return await Collection.UpdateOneAsync(d => d.Id == id, update);
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