using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Tenants;

public interface IDeploymentsService
{
    public Task<DeploymentsPage> FindLatest(string? environment, int offset = 0, int page = 0, int size = 0,
        CancellationToken cancellationToken = new());

    public Task<SquashedDeploymentsPage> FindLatestSquashed(string? environment, int page = 0,
        int size = 0,
        CancellationToken cancellationToken = new());

    public Task<List<Deployment>> FindWhatsRunningWhere(List<string> environments,
        CancellationToken cancellationToken);

    public Task<List<Deployment>> FindWhatsRunningWhere(string serviceName, CancellationToken cancellationToken);

    public Task<Deployment?> FindDeployment(string deploymentId, CancellationToken cancellationToken);

    public Task<Deployment?> FindDeploymentByEcsSvcDeploymentId(string ecsSvcDeploymentId,
        CancellationToken cancellationToken);

    public Task<UpdateResult> LinkRequestedDeployment(ObjectId? id, Deployment deployment,
        CancellationToken cancellationToken);

    public Task Insert(Deployment deployment, CancellationToken cancellationToken);
    Task<List<Deployment>> FindDeployments(string deploymentId, CancellationToken cancellationToken);

    public Task<DeploymentSettings?> FindDeploymentConfig(string service, string environment,
        CancellationToken cancellationToken);
}

public class DeploymentsService : MongoService<Deployment>, IDeploymentsService
{
    private const string CollectionName = "deployments";
    public static readonly int DefaultPageSize = 50;
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

    public async Task<SquashedDeploymentsPage> FindLatestSquashed(string? environment, int page = 1, int size = 50,
        CancellationToken cancellationToken = new())
    {
        var skip = size * (page - 1);
        var limit = size;
        var fd = new FilterDefinitionBuilder<Deployment>();
        var environmentFilter = string.IsNullOrWhiteSpace(environment)
            ? fd.Empty
            : fd.And(
                fd.Eq(d => d.Environment, environment),
                fd.Ne(d => d.EcsSvcDeploymentId, null)
            );

        var pipeline = new EmptyPipelineDefinition<Deployment>()
            .Match(environmentFilter)
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt))
            .Group(
                d => d.EcsSvcDeploymentId,
                grp =>
                    new SquashedDeployment
                    {
                        CreatedAt = grp.Last().DeployedAt,
                        UpdatedAt = grp.First().DeployedAt,
                        DeploymentId = grp.First().DeploymentId,
                        DockerImage = grp.First().DockerImage,
                        Environment = grp.First().Environment,
                        RequestedCount = grp.First().InstanceCount,
                        Service = grp.First().Service,
                        Version = grp.First().Version,
                        Status = grp.First().Status,
                        DesiredStatus = grp.First().DesiredStatus,
                        TaskId = grp.First().TaskId,
                        User = grp.First().User,
                        UserId = grp.First().UserId,
                        Cpu = grp.First().Cpu,
                        Memory = grp.First().Memory
                    })
            .Project(p => p)
            .Sort(new SortDefinitionBuilder<SquashedDeployment>().Descending(d => d.CreatedAt));

        var result = await Collection.Aggregate(
                pipeline.Skip(skip).Limit(limit), cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        var totalSquashedDeployments = await Collection.Aggregate(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);

        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalSquashedDeployments.Count() / size));

        return new SquashedDeploymentsPage(result!, page, size, totalPages);
    }

    public Task<Deployment?> FindDeployment(string deploymentId, CancellationToken cancellationToken)
    {
        return Collection.Find(d => d.DeploymentId == deploymentId).SortBy(d => d.DeployedAt)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    public async Task<List<Deployment>> FindWhatsRunningWhere(string serviceName, CancellationToken cancellationToken)
    {
        var pipeline = new EmptyPipelineDefinition<Deployment>()
            .Match(d => d.Service == serviceName && d.Status == "RUNNING" && d.DesiredStatus != "STOPPED")
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

    public async Task<List<Deployment>> FindDeployments(string deploymentId, CancellationToken cancellationToken)
    {
        var filter = new FilterDefinitionBuilder<Deployment>().Eq(d => d.DeploymentId, deploymentId);
        var result = await Collection.Find(filter).ToListAsync(cancellationToken);

        return result;
    }

    public async Task<DeploymentSettings?> FindDeploymentConfig(string service, string environment,
        CancellationToken cancellationToken)
    {
        var fb = new FilterDefinitionBuilder<Deployment>();
        var filter = fb.And(fb.Eq(d => d.Service, service), fb.Eq(d => d.Environment, environment));
        var sort = new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt);

        return await Collection
            .Find(d => d.Environment == environment && d.Service == service && d.Status == "REQUESTED")
            .Sort(sort)
            .Project(d => new DeploymentSettings { Cpu = d.Cpu, Memory = d.Memory, InstanceCount = d.InstanceCount })
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<Deployment?> FindDeploymentByEcsSvcDeploymentId(string ecsSvcDeploymentId,
        CancellationToken cancellationToken)
    {
        return await Collection
            .Find(d => d.EcsSvcDeploymentId == ecsSvcDeploymentId)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<List<Deployment>> FindWhatsRunningWhere(List<string> environments, CancellationToken
        cancellationToken)
    {
        var fd = new FilterDefinitionBuilder<Deployment>();
        var environmentsFilter = environments.Any()
            ? fd.And(
                fd.Eq(d => d.Status, "RUNNING"),
                fd.Where(d => environments.Contains(d.Environment)),
                fd.Ne(d => d.DesiredStatus, "STOPPED")
            )
            : fd.And(
                fd.Eq(d => d.Status, "RUNNING"),
                fd.Ne(d => d.DesiredStatus, "STOPPED")
            );


        var pipeline = new EmptyPipelineDefinition<Deployment>()
            .Match(environmentsFilter)
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.DeployedAt))
            .Group(d => new { d.Service, d.Environment }, grp => new { Root = grp.First() })
            .Project(grp => grp.Root);

        return await Collection.Aggregate(pipeline, cancellationToken: cancellationToken)
            .ToListAsync(cancellationToken);
    }

    protected override List<CreateIndexModel<Deployment>> DefineIndexes(
        IndexKeysDefinitionBuilder<Deployment> builder)
    {
        var nameAndEnv = new CreateIndexModel<Deployment>(builder.Combine(
            builder.Ascending(d => d.Environment),
            builder.Ascending(d => d.Service)));
        var deploymentTime = new CreateIndexModel<Deployment>(builder.Descending(d => d.DeployedAt));
        var taskAndInstanceId = new CreateIndexModel<Deployment>(
            builder.Combine(builder.Ascending(d => d.TaskId), builder.Ascending(d => d.InstanceTaskId)),
            new CreateIndexOptions { Sparse = true });
        var ecsDeploymentId = new CreateIndexModel<Deployment>(builder.Descending(d => d.EcsSvcDeploymentId));
        
        return new List<CreateIndexModel<Deployment>> { nameAndEnv, deploymentTime, taskAndInstanceId, ecsDeploymentId };
    }
}