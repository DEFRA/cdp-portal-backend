using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using MongoDB.Bson;
using MongoDB.Driver;
using static Defra.Cdp.Backend.Api.Services.Aws.Deployments.DeploymentStatus;

namespace Defra.Cdp.Backend.Api.Services.Deployments;

public interface IDeploymentsServiceV2
{
    Task                RegisterDeployment(RequestedDeployment req, CancellationToken ct);
    Task<bool>          LinkDeployment(string cdpId, string lambdaId, CancellationToken ct);
    Task                UpdateDeployment(DeploymentV2 deployment, CancellationToken ct);
    Task<DeploymentV2?> FindDeploymentByLambdaId(string lambdaId, CancellationToken ct);
  
    Task<Paginated<DeploymentV2>> FindLatest(
        string? environment,
        string? service,
        string? user,
        string? status,
        int offset = 0,
        int page = 0,
        int size = 0,
        CancellationToken ct = new()
    );
    Task<DeploymentV2?> FindDeployment(string deploymentId, CancellationToken ct);

    Task<List<DeploymentV2>> FindWhatsRunningWhere(List<string> environments, CancellationToken ct);
    Task<List<DeploymentV2>> FindWhatsRunningWhere(string serviceName, CancellationToken ct);
    Task<DeploymentFilters> GetFilters(CancellationToken ct);
    Task<DeploymentSettings?> FindDeploymentConfig(string service, string environment, CancellationToken ct);
}

public class DeploymentsServiceV2 : MongoService<DeploymentV2>, IDeploymentsServiceV2
{
    public static readonly int DefaultPageSize = 50;
    public static readonly int DefaultPage = 1;
    
    public DeploymentsServiceV2(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(connectionFactory, "deploymentsV2", loggerFactory)
    {
    }

    protected override List<CreateIndexModel<DeploymentV2>> DefineIndexes(IndexKeysDefinitionBuilder<DeploymentV2> builder)
    {
        var created           = new CreateIndexModel<DeploymentV2>(builder.Descending(d => d.Created));
        var updated           = new CreateIndexModel<DeploymentV2>(builder.Descending(d => d.Updated));
        var lambdaId          = new CreateIndexModel<DeploymentV2>(builder.Descending(d => d.LambdaId));
        var cdpDeploymentId   = new CreateIndexModel<DeploymentV2>(builder.Descending(d => d.CdpDeploymentId));
        var envServiceVersion = new CreateIndexModel<DeploymentV2>(builder.Combine(
            builder.Descending(d => d.Environment),
            builder.Descending(d => d.Service),
            builder.Descending(d => d.Version)
        ));
        
        return new List<CreateIndexModel<DeploymentV2>> { created, updated, lambdaId, cdpDeploymentId, envServiceVersion };
    }

    public async Task RegisterDeployment(RequestedDeployment req, CancellationToken ct)
    {
        await Collection.InsertOneAsync(DeploymentV2.FromRequest(req), null, ct);
    }

    public async Task<bool> LinkDeployment(string cdpId, string lambdaId, CancellationToken ct)
    {
        // Before we can start recording events, we need to match the CDP Id (which is generated in the portal)
        // to the ID of the lambda that triggered the deployment (lambda Id) which will appear in subsequent ECS messages
        // in the `startedBy` field.
        var update = new UpdateDefinitionBuilder<DeploymentV2>().Set(d => d.LambdaId, lambdaId);
        var result = await Collection.UpdateOneAsync(d => d.CdpDeploymentId == cdpId, update, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }
    
    public async Task<DeploymentV2?> FindDeploymentByLambdaId(string lambdaId, CancellationToken ct)
    {
        return await Collection.Find(d => d.LambdaId == lambdaId).FirstOrDefaultAsync(ct);
    }
    
    public async Task<List<DeploymentV2>> FindWhatsRunningWhere(List<string> environments, CancellationToken ct)
    {
        var fb = new FilterDefinitionBuilder<DeploymentV2>();

        var statusFilter = fb.In(d => d.Status, new[] { Running, Pending, Undeployed });
        var envFilter = fb.Empty;
        if (environments.Any())
        {
            envFilter = fb.In(d => d.Environment, environments);
        }
        var filter = fb.And(envFilter, statusFilter);

        var pipeline = new EmptyPipelineDefinition<DeploymentV2>()
            .Match(filter)
            .Sort(new SortDefinitionBuilder<DeploymentV2>().Descending(d => d.Updated))
            .Group(d => new { d.Service, d.Environment }, grp => new { Root = grp.First() })
            .Project(grp => grp.Root);

        return await Collection.Aggregate(pipeline, cancellationToken: ct).ToListAsync(ct);
    }

    public async Task<List<DeploymentV2>> FindWhatsRunningWhere(string serviceName, CancellationToken ct)
    {
        var fb = new FilterDefinitionBuilder<DeploymentV2>();
        var filter = fb.And(
            fb.Eq(d => d.Service, serviceName),
            fb.In(d => d.Status, new[] { Running, Pending, Undeployed })
        );
        var pipeline = new EmptyPipelineDefinition<DeploymentV2>()
            .Match(filter)
            .Sort(new SortDefinitionBuilder<DeploymentV2>().Descending(d => d.Updated))
            .Group(d => new { d.Environment }, grp => new { Root = grp.First() })
            .Project(grp => grp.Root);

        return await Collection.Aggregate(pipeline, cancellationToken: ct)
            .ToListAsync(ct);
    }

    public async Task<Paginated<DeploymentV2>> FindLatest(string? environment, string? service, string? user,
        string? status,
        int offset = 0,
        int page = 0,
        int size = 0,
        CancellationToken ct = new()
    )
    {
        var builder = Builders<DeploymentV2>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(environment))
        {
            var environmentFilter = builder.Eq(d => d.Environment, environment);
            filter &= environmentFilter;
        }

        if (!string.IsNullOrWhiteSpace(service))
        {
            var serviceFilter = builder.Regex(d => d.Service,
                new BsonRegularExpression(service, "i"));
            filter &= serviceFilter;
        }

        if (!string.IsNullOrWhiteSpace(user))
        {
            var userFilter = builder.Where(d =>
                (d.User != null && d.User.Id == user)
                || (d.User != null && d.User.DisplayName.ToLower().Contains(user.ToLower())));
            filter &= userFilter;
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusLower = status.ToLower();
            var statusFilter = builder.Where(d =>
                d.Status.ToLower() == statusLower || d.Status.ToLower().Contains(statusLower));
            filter &= statusFilter;
        }

        var deployments = await Collection
            .Find(filter)
            .Skip(offset + size * (page - DefaultPage))
            .Limit(size)
            .SortByDescending(d => d.Created)
            .ToListAsync(ct);

        var totalDeployments = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalDeployments / size));

        return new Paginated<DeploymentV2>(deployments, page, size, totalPages);
    }

    public async Task<DeploymentV2?> FindDeployment(string deploymentId, CancellationToken ct)
    {
        return await Collection.Find(d => d.CdpDeploymentId == deploymentId).FirstOrDefaultAsync(ct);
    }

    public async Task<DeploymentSettings?> FindDeploymentConfig(string service, string environment, CancellationToken ct)
    {
        var fb = new FilterDefinitionBuilder<DeploymentV2>();
        var filter = fb.And(fb.Eq(d => d.Service, service), fb.Eq(d => d.Environment, environment));
        var sort = new SortDefinitionBuilder<DeploymentV2>().Descending(d => d.Created);

        return await Collection
            .Find(filter)
            .Sort(sort)
            .Project(d => new DeploymentSettings { Cpu = d.Cpu, Memory = d.Memory, InstanceCount = d.InstanceCount })
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpdateDeployment(DeploymentV2 deployment, CancellationToken ct)
    {
        await Collection.ReplaceOneAsync(d => d.Id == deployment.Id, deployment, cancellationToken: ct);
    }

    public async Task<DeploymentFilters> GetFilters(CancellationToken ct = new())
    {
        var serviceNames = await Collection
            .Distinct(d => d.Service, FilterDefinition<DeploymentV2>.Empty, cancellationToken: ct)
            .ToListAsync(ct);

        var statuses = await Collection
            .Distinct(d => d.Status, FilterDefinition<DeploymentV2>.Empty, cancellationToken: ct)
            .ToListAsync(ct);

        var userFilter =
            new FilterDefinitionBuilder<DeploymentV2>().Where(d => d.User != null && d.User.DisplayName != "n/a");
        var users = await Collection
            .Distinct<DeploymentV2, UserDetails>(d => d.User!, userFilter, cancellationToken: ct)
            .ToListAsync(ct);


        return new DeploymentFilters { Services = serviceNames, Users = users, Statuses = statuses };
    }
}