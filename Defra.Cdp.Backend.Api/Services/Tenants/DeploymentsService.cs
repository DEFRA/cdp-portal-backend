using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Tenants;

public interface IDeploymentsService
{
    Task<List<Deployment>> FindLatest(int offset = 0, CancellationToken cancellationToken = new());

    public Task<DeploymentsPage> FindLatest(string? environment, int offset = 0, int page = 0, int size = 0,
        CancellationToken cancellationToken = new());

    public Task<SquashedDeploymentsPage> FindLatestSquashed(string? environment, int offset = 0, int page = 0,
        int size = 0,
        CancellationToken cancellationToken = new());

    public Task<List<Deployment>> FindWhatsRunningWhere(CancellationToken cancellationToken);
    public Task<List<Deployment>> FindWhatsRunningWhere(string serviceName, CancellationToken cancellationToken);
    public Task<Deployment?> FindDeployment(string deploymentId, CancellationToken cancellationToken);

    public Task<Deployment?> FindDeploymentByEcsSvcDeploymentId(string ecsSvcDeploymentId,
        CancellationToken cancellationToken);

    public Task<Deployment?> FindRequestedDeployment(string service, string version, string environment,
        DateTime deployedAt, string deploymentId, CancellationToken cancellationToken);

    public Task<UpdateResult> LinkRequestedDeployment(ObjectId? id, Deployment deployment,
        CancellationToken cancellationToken);

    public Task Insert(Deployment deployment, CancellationToken cancellationToken);
}

public class DeploymentsService : MongoService<Deployment>, IDeploymentsService
{
    private const string CollectionName = "deployments";
    public static readonly int DefaultPageSize = 20;
    public static readonly int DefaultPage = 1;

    public DeploymentsService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(
        connectionFactory,
        CollectionName, loggerFactory)
    {
    }

    public async Task<DeploymentsPage> FindLatest(string? environment, int offset = 0, int page = 0, int size = 0,
        CancellationToken cancellationToken = new())
    {
        page = page == 0 ? DefaultPage : page;
        size = size == 0 ? DefaultPageSize : size;
        if (size <= 0) throw new ArgumentException("page size cannot be less than zero");
        var filterDefinition = string.IsNullOrWhiteSpace(environment)
            ? FilterDefinition<Deployment>.Empty
            : new FilterDefinitionBuilder<Deployment>().Where(d => d.Environment == environment);

        var deployments = await Collection
            .Find(filterDefinition)
            .Skip(offset + size * (page - DefaultPage))
            .Limit(size)
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt))
            .ToListAsync(cancellationToken);

        var totalDeployments = await Collection.CountDocumentsAsync(filterDefinition,
            cancellationToken: cancellationToken);

        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalDeployments / size));

        return new DeploymentsPage(deployments, page, size, totalPages);
    }

    public async Task<SquashedDeploymentsPage> FindLatestSquashed(string? environment, int offset = 0, int page = 0,
        int size = 0,
        CancellationToken cancellationToken = new())
    {
        page = page == 0 ? DefaultPage : page;
        size = size == 0 ? DefaultPageSize : size;
        if (size <= 0) throw new ArgumentException("page size cannot be less than zero");
        var pipeline = new EmptyPipelineDefinition<Deployment>()
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt))
            .Group(
                d => new { d.Status, d.TaskId, d.InstanceCount },
                grp =>
                    new
                    {
                        Root = new SquashedDeployment
                        {
                            Count = grp.Count(),
                            DeployedAt = grp.Max(d => d.DeployedAt),
                            DeploymentId = grp.First().DeploymentId,
                            DockerImage = grp.First().DockerImage,
                            Environment = grp.First().Environment,
                            RequestedCount = grp.First().InstanceCount,
                            Service = grp.First().Service,
                            Version = grp.First().Version,
                            Status = grp.First().Status,
                            TaskId = grp.First().TaskId,
                            User = grp.First().User,
                            UserId = grp.First().UserId
                        }
                    })
            .Project(p => p.Root);

        var result = await Collection.Aggregate(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        return new SquashedDeploymentsPage(result!, page, size, 100);
    }

    public Task<Deployment?> FindDeployment(string deploymentId, CancellationToken cancellationToken)
    {
        return Collection.Find(d => d.DeploymentId == deploymentId).SortBy(d => d.DeployedAt)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    // Used to join up an incoming ECS request with requested deployment from cdp-self-service-ops
    // We look for any task that matches the name/version/env and happened up to 30 minutes before the first deployment 
    // event. 
    public async Task<Deployment?> FindRequestedDeployment(string service, string version, string environment,
        DateTime timestamp, string deploymentId, CancellationToken cancellationToken)
    {
        // See if we've already matched the requested deployment via deployment id
        var requested = await Collection.Find(d => d.DeploymentId == deploymentId && d.Status == "REQUESTED")
            .FirstOrDefaultAsync(cancellationToken);

        if (requested != null) return requested;

        var filterBuilder = Builders<Deployment>.Filter;

        var filter = filterBuilder.And(
            filterBuilder.Eq(d => d.Service, service),
            filterBuilder.Eq(d => d.Version, version),
            filterBuilder.Eq(d => d.Environment, environment),
            filterBuilder.Eq(d => d.DeploymentId, null),
            filterBuilder.Gt(d => d.DeployedAt, timestamp.AddMinutes(-30)),
            filterBuilder.Lte(d => d.DeployedAt, timestamp)
        );

        return await Collection.Find(filter).SortBy(d => d.DeployedAt).FirstOrDefaultAsync(cancellationToken)!;
    }

    public async Task<List<Deployment>> FindLatest(int offset = 0, CancellationToken cancellationToken = new())
    {
        return await Collection
            .Find(FilterDefinition<Deployment>.Empty)
            .Skip(offset)
            .Limit(200)
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt)).ToListAsync(cancellationToken);
    }

    public async Task<List<Deployment>> FindWhatsRunningWhere(string serviceName, CancellationToken cancellationToken)
    {
        var pipeline = new EmptyPipelineDefinition<Deployment>().Match(d => d.Service == serviceName)
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt))
            .Group(d => new { d.Environment }, grp => new { Root = grp.First() })
            .Project(grp => grp.Root);

        return await Collection.Aggregate(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);
    }

    public async Task<UpdateResult> LinkRequestedDeployment(ObjectId? id, Deployment deployment,
        CancellationToken cancellationToken)
    {
        var update = Builders<Deployment>
            .Update
            .Set(d => d.TaskId, deployment.TaskId).Set(d => d.DeploymentId, deployment.DeploymentId).Set(
                d => d.DockerImage,
                deployment.DockerImage);

        return await Collection.UpdateOneAsync(d => d.Id == id, update, cancellationToken: cancellationToken);
    }

    public async Task Insert(Deployment deployment, CancellationToken cancellationToken)
    {
        if (deployment.Id != null)
            await Collection.ReplaceOneAsync(d => d.Id == deployment.Id, deployment,
                cancellationToken: cancellationToken);
        else
            await Collection.InsertOneAsync(deployment, cancellationToken: cancellationToken);
    }

    public async Task<List<Deployment>> FindWhatsRunningWhere(CancellationToken cancellationToken)
    {
        var pipeline = new EmptyPipelineDefinition<Deployment>()
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt))
            .Group(d => new { d.Service, d.Environment }, grp => new { Root = grp.First() })
            .Project(grp => grp.Root);

        return await Collection.Aggregate(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);
    }

    public async Task<Deployment?> FindDeploymentByEcsSvcDeploymentId(string ecsSvcDeploymentId,
        CancellationToken cancellationToken)
    {
        return await Collection
            .Find(d => d.EcsSvcDeploymentId == ecsSvcDeploymentId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    protected override List<CreateIndexModel<Deployment>> DefineIndexes(
        IndexKeysDefinitionBuilder<Deployment> builder)
    {
        var indexModel = new CreateIndexModel<Deployment>(builder.Combine(
            builder.Ascending(d => d.Environment),
            builder.Ascending(d => d.Service)));
        var titleModel = new CreateIndexModel<Deployment>(builder.Descending(d => d.DeployedAt));
        var instanceCount = new CreateIndexModel<Deployment>(builder.Descending(d => d.InstanceCount),
            new CreateIndexOptions { Sparse = true });
        var task = new CreateIndexModel<Deployment>(
            builder.Combine(builder.Ascending(d => d.TaskId), builder.Ascending(d => d.InstanceTaskId)),
            new CreateIndexOptions { Sparse = true });
        return new List<CreateIndexModel<Deployment>> { indexModel, titleModel, instanceCount, task };
    }
}