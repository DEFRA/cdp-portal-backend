using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Aws.AutoDeploymentTriggers;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using MongoDB.Bson;
using MongoDB.Driver;
using static Defra.Cdp.Backend.Api.Services.Aws.Deployments.DeploymentStatus;

namespace Defra.Cdp.Backend.Api.Services.Deployments;

public interface IDeploymentsServiceV2
{
    Task                RegisterDeployment(DeploymentV2 deployment, CancellationToken ct);
    Task<bool>          LinkDeployment(string cdpId, string lambdaId, CancellationToken ct);
    Task                UpdateDeployment(DeploymentV2 deployment, CancellationToken ct);
    Task<DeploymentV2?> FindDeploymentByLambdaId(string lambdaId, CancellationToken ct);
    Task<bool>          UpdateDeploymentStatus(string lambdaId, string eventName, string reason, CancellationToken ct);
    
    Task<Paginated<DeploymentV2>> FindLatest(
        string[]? favouriteTeamIds,
        string? environment,
        string? service,
        string? user,
        string? status,
        string? team,
        int offset = 0,
        int page = 0,
        int size = 0,
        CancellationToken ct = new()
    );
    Task<DeploymentV2?> FindDeployment(string deploymentId, CancellationToken ct);
    Task<DeploymentV2?> FindDeploymentByTaskArn(string taskArn, CancellationToken ct);

    Task<List<DeploymentV2>> FindWhatsRunningWhere(string[]? environments, string? service, string? team,
        string? user, string? status, CancellationToken ct);
    Task<List<DeploymentV2>> FindWhatsRunningWhere(string serviceName, CancellationToken ct);
    Task<DeploymentFilters> GetWhatsRunningWhereFilters(CancellationToken ct);
    Task<DeploymentFilters> GetDeploymentsFilters(CancellationToken ct);
    Task<DeploymentSettings?> FindDeploymentSettings(string service, string environment, CancellationToken ct);
    Task Decommission(string serviceName, CancellationToken ct);
}

public class DeploymentsServiceV2 : MongoService<DeploymentV2>, IDeploymentsServiceV2
{
    public static readonly int DefaultPageSize = 50;
    public static readonly int DefaultPage = 1;
    private readonly IRepositoryService _repositoryService;
    private readonly IUserServiceFetcher _userServiceFetcher;
    private readonly HashSet<string> _excludedDisplayNames =
        new(StringComparer.CurrentCultureIgnoreCase) { "n/a", "admin", "GitHub Workflow" };
    
    public DeploymentsServiceV2(
        IMongoDbClientFactory connectionFactory, 
        IRepositoryService repositoryService, 
        IUserServiceFetcher userServiceFetcher, 
        ILoggerFactory loggerFactory) : base(connectionFactory, "deploymentsV2", loggerFactory)
    {
        _repositoryService = repositoryService;
        _userServiceFetcher = userServiceFetcher;
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
        
        return [created, updated, lambdaId, cdpDeploymentId, envServiceVersion];
    }

    public async Task RegisterDeployment(DeploymentV2 deployment, CancellationToken ct)
    {
        await Collection.InsertOneAsync(await WithAuditData(deployment, ct), null, ct);
    }

    private async Task<DeploymentV2> WithAuditData(DeploymentV2 deployment, CancellationToken ct)
    {
        deployment.Audit = new Audit();
        // Record who owned the service at that point in time
        try
        {
            var repo = await _repositoryService.FindRepositoryById(deployment.Service, ct);
            var teams = repo?.Teams ?? [];
            deployment.Audit.ServiceOwners = teams.ToList();
        }
        catch (Exception ex)
        {
            Logger.LogError("Failed to lookup teams for {services}, {ex}", deployment.Service, ex);
        }
        
        // Record which teams the user belonged to at that point in time unless its an auto-deployment
        if (deployment.User?.Id != null && 
            deployment.User.Id != AutoDeploymentConstants.AutoDeploymentId)
        {
            try
            {
                var user = await _userServiceFetcher.GetUser(deployment.User.Id, ct);
                deployment.Audit.User = user?.user;
            }
            catch (Exception ex)
            {
                Logger.LogError("Failed to lookup user for {userId}, {ex}", deployment.User?.Id, ex);
            }
        }
        return deployment;
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

    public async Task<bool> UpdateDeploymentStatus(string lambdaId, string eventName, string reason, CancellationToken ct)
    {
        var deployment = await FindDeploymentByLambdaId(lambdaId, ct);

        var update = new UpdateDefinitionBuilder<DeploymentV2>()
            .Set(d => d.LastDeploymentStatus, eventName)
            .Set(d => d.LastDeploymentMessage, reason);
        
        if (deployment != null)
        {
            deployment.LastDeploymentStatus = eventName;
            update = update.Set(d => d.Status, CalculateOverallStatus(deployment));
        }
            
        var result = await Collection.UpdateOneAsync(d => d.LambdaId == lambdaId, update, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task<DeploymentV2?>  FindDeploymentByTaskArn(string taskArn, CancellationToken ct)
    {
        var fb = new FilterDefinitionBuilder<DeploymentV2>();
        var filter = fb.Eq(d => d.TaskDefinitionArn, taskArn);
        var latestFirst = new SortDefinitionBuilder<DeploymentV2>().Descending(d => d.Created);
        return await Collection.Find(filter).Sort(latestFirst).FirstOrDefaultAsync(ct);
    }

    public async Task<List<DeploymentV2>> FindWhatsRunningWhere(string[]? environments, string? service, string? team,
        string? user, string? status, CancellationToken ct)

    {
        var builder = Builders<DeploymentV2>.Filter;
        var filter = builder.In(d => d.Status, [Running, Pending, Undeployed]);

        if (environments?.Length > 0)
        {
            var envFilter = builder.In(d => d.Environment, environments);
            filter &= envFilter;
        }

        if (!string.IsNullOrWhiteSpace(team))
        {
            var repos = await _repositoryService.FindRepositoriesByTeamId(team, true, ct);
            var servicesOwnedByTeam = repos.Select(r => r.Id);
            var teamFilter = builder.In(d => d.Service, servicesOwnedByTeam);
            filter &= teamFilter;
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

        if (!string.IsNullOrWhiteSpace(service))
        {
            var partialServiceFilter =
                Builders<DeploymentV2>.Filter.Regex(d => d.Service,  new BsonRegularExpression(service, "i"));
            filter &= partialServiceFilter;
        }
        
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

    public async Task<Paginated<DeploymentV2>> FindLatest(string[]? favouriteTeamIds, string? environment,
        string? service, string? user,
        string? status,
        string? team,
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

        if (!string.IsNullOrWhiteSpace(team))
        {
            var repos = await _repositoryService.FindRepositoriesByTeamId(team, true, ct);
            var servicesOwnedByTeam = repos.Select(r => r.Id);
            var teamFilter = builder.In(d => d.Service, servicesOwnedByTeam);
            filter &= teamFilter;
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

        if (favouriteTeamIds?.Length > 0)
        {
            var repos = (await Task.WhenAll(favouriteTeamIds.Select(teamId =>
                    _repositoryService.FindRepositoriesByTeamId(teamId, true, ct))))
                .SelectMany(r => r)
                .ToList();

            var servicesOwnedByTeam = repos.Select(r => r.Id);

            deployments = deployments.OrderByDescending(d => servicesOwnedByTeam.Contains(d.Service)).ToList();
        }

        var totalDeployments = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalDeployments / size));

        return new Paginated<DeploymentV2>(deployments, page, size, totalPages);
    }

    public async Task<DeploymentV2?> FindDeployment(string deploymentId, CancellationToken ct)
    {
        return await Collection.Find(d => d.CdpDeploymentId == deploymentId).FirstOrDefaultAsync(ct);
    }

    public async Task<DeploymentSettings?> FindDeploymentSettings(string service, string environment, CancellationToken ct)
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

    public async Task Decommission(string serviceName, CancellationToken ct)
    {
        await Collection.DeleteManyAsync(d => d.Service == serviceName, ct);
    }

    public async Task UpdateDeployment(DeploymentV2 deployment, CancellationToken ct)
    {
        await Collection.ReplaceOneAsync(d => d.Id == deployment.Id, deployment, cancellationToken: ct);
    }

    private async Task<List<UserDetails>> UserDetailsList(CancellationToken ct)
    {
        var userFilter = Builders<DeploymentV2>.Filter.And(
            Builders<DeploymentV2>.Filter.Ne(d => d.User, null),
            Builders<DeploymentV2>.Filter.Nin(d => d.User!.DisplayName, _excludedDisplayNames)
        );

        var users = await Collection
            .Distinct<DeploymentV2, UserDetails>(d => d.User!, userFilter, cancellationToken: ct)
            .ToListAsync(ct);

        users.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));
        
        return users;
    }

    public async Task<DeploymentFilters> GetDeploymentsFilters(CancellationToken ct)
    {
        var serviceNames = await Collection
            .Distinct(d => d.Service, FilterDefinition<DeploymentV2>.Empty, cancellationToken: ct)
            .ToListAsync(ct);

        var statuses = await Collection
            .Distinct(d => d.Status, FilterDefinition<DeploymentV2>.Empty, cancellationToken: ct)
            .ToListAsync(ct);

        var users = await UserDetailsList(ct);

        serviceNames.Sort();
        statuses.Sort();

        return new DeploymentFilters { Services = serviceNames, Users = users, Statuses = statuses };
    }

    public async Task<DeploymentFilters> GetWhatsRunningWhereFilters(CancellationToken ct)
    {
        var builder = Builders<DeploymentV2>.Filter;
        var filter = builder.In(d => d.Status, [Running, Pending, Undeployed]);

        var serviceNames = await Collection
            .Distinct(d => d.Service, filter, cancellationToken: ct)
            .ToListAsync(ct);

        var statuses = await Collection
            .Distinct(d => d.Status, filter, cancellationToken: ct)
            .ToListAsync(ct);

        var users = await UserDetailsList(ct);

        serviceNames.Sort();
        statuses.Sort();

        return new DeploymentFilters { Services = serviceNames, Users = users, Statuses = statuses };
    }
}