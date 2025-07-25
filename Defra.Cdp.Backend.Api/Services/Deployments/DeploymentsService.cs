using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.AutoDeploymentTriggers;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.Migrations;
using MongoDB.Bson;
using MongoDB.Driver;
using static Defra.Cdp.Backend.Api.Services.Aws.Deployments.DeploymentStatus;

namespace Defra.Cdp.Backend.Api.Services.Deployments;

public interface IDeploymentsService
{
    Task RegisterDeployment(Deployment deployment, CancellationToken ct);
    Task<bool> LinkDeployment(string cdpId, string lambdaId, CancellationToken ct);
    Task UpdateOverallTaskStatus(Deployment deployment, CancellationToken ct);
    Task<Deployment?> FindDeploymentByLambdaId(string lambdaId, CancellationToken ct);
    Task<bool> UpdateDeploymentStatus(string lambdaId, string eventName, string reason, CancellationToken ct);
    Task UpdateInstance(string cdpDeploymentId, string instanceId, DeploymentInstanceStatus instanceStatus, CancellationToken ct);
    
    Task<Paginated<Deployment>> FindLatest(
        string[]? favourites,
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


    Task<Paginated<DeploymentOrMigration>> FindLatestWithMigrations(
        string[]? favourites,
        string? environment,
        string? service,
        string? user,
        string? status,
        string? team,
        string? kind,
        int offset = 0,
        int page = 0,
        int size = 0,
        CancellationToken ct = new()
    );

    Task<Deployment?> FindDeployment(string deploymentId, CancellationToken ct);
    Task<Deployment?> FindDeploymentByTaskArn(string taskArn, CancellationToken ct);

    Task<List<Deployment>> RunningDeploymentsForService(string[]? environments, string? service, string? team,
        string? user, string? status, CancellationToken ct);
    Task<List<Deployment>> RunningDeploymentsForService(string serviceName, CancellationToken ct);
    Task<DeploymentFilters> GetWhatsRunningWhereFilters(CancellationToken ct);
    Task<DeploymentFilters> GetDeploymentsFilters(CancellationToken ct);
    Task<DeploymentSettings?> FindDeploymentSettings(string service, string environment, CancellationToken ct);
}

public class DeploymentsService(
    IMongoDbClientFactory connectionFactory,
    IRepositoryService repositoryService,
    IUserServiceFetcher userServiceFetcher,
    ILoggerFactory loggerFactory)
    : MongoService<Deployment>(connectionFactory, CollectionName, loggerFactory), IDeploymentsService
{
    public const string CollectionName = "deploymentsV2";
    public const int DefaultPageSize = 50;
    public const int DefaultPage = 1;
    private const string Migration = "migration";
    private const string Deployment = "deployment";
    
    private readonly TimeSpan _requestTimeout = TimeSpan.FromMinutes(20);

    private readonly HashSet<string> _excludedDisplayNames =
        new(StringComparer.CurrentCultureIgnoreCase) { "n/a", "admin", "GitHub Workflow" };

    protected override List<CreateIndexModel<Deployment>> DefineIndexes(IndexKeysDefinitionBuilder<Deployment> builder)
    {
        var created = new CreateIndexModel<Deployment>(builder.Descending(d => d.Created));
        var updated = new CreateIndexModel<Deployment>(builder.Descending(d => d.Updated));
        var lambdaId = new CreateIndexModel<Deployment>(builder.Descending(d => d.LambdaId));
        var cdpDeploymentId = new CreateIndexModel<Deployment>(builder.Descending(d => d.CdpDeploymentId));
        var envServiceVersion = new CreateIndexModel<Deployment>(builder.Combine(
            builder.Descending(d => d.Environment),
            builder.Descending(d => d.Service),
            builder.Descending(d => d.Version)
        ));

        return [created, updated, lambdaId, cdpDeploymentId, envServiceVersion];
    }

    public async Task RegisterDeployment(Deployment deployment, CancellationToken ct)
    {
        if (deployment.Status == Requested)
        {
            await CleanupRequestedDeployments(deployment.Service, deployment.Environment, ct);
        }
        
        await Collection.InsertOneAsync(await WithAuditData(deployment, ct), null, ct);
    }

    /**
     * Updates any previous deployments for this service, in this environment that are still in requested after 20 mins.
     * These records exist because either:
     * - The lambda never actioned the request
     * - The control plane attempted several deployments of the same service at once.
     */
    private async Task CleanupRequestedDeployments(string service, string environment, CancellationToken ct)
    {
        var fb = new FilterDefinitionBuilder<Deployment>();
        var filter = fb.And(
            fb.Eq(d => d.Service, service), 
            fb.Eq(d => d.Environment, environment),
            fb.Eq(d => d.Status, Requested),
            fb.Lt(d => d.Created, DateTime.UtcNow.Subtract(_requestTimeout))
        );

        var update = new UpdateDefinitionBuilder<Deployment>()
            .Set(d => d.Status, Failed)
            .Set(d => d.LastDeploymentStatus, SERVICE_DEPLOYMENT_FAILED)
            .Set(d => d.LastDeploymentMessage, "Deployment timed out.");

        var options = new UpdateOptions { IsUpsert = false, };

        var result = await Collection.UpdateManyAsync(filter, update, options, ct);
        if (result.ModifiedCount > 0)
        {
            Logger.LogInformation("Removed {Count} stuck deployments for {Service} in {Environment}",
                result.ModifiedCount, service, environment);
        }
    }

    private async Task<Deployment> WithAuditData(Deployment deployment, CancellationToken ct)
    {
        deployment.Audit = new Audit();
        // Record who owned the service at that point in time
        try
        {
            var repo = await repositoryService.FindRepositoryById(deployment.Service, ct);
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
                var user = await userServiceFetcher.GetUser(deployment.User.Id, ct);
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
        var update = new UpdateDefinitionBuilder<Deployment>().Set(d => d.LambdaId, lambdaId);
        var result = await Collection.UpdateOneAsync(d => d.CdpDeploymentId == cdpId, update, cancellationToken: ct);
        return result.ModifiedCount == 1;
    }

    public async Task<Deployment?> FindDeploymentByLambdaId(string lambdaId, CancellationToken ct)
    {
        return await Collection.Find(d => d.LambdaId == lambdaId).FirstOrDefaultAsync(ct);
    }

    public async Task<bool> UpdateDeploymentStatus(string lambdaId, string eventName, string reason, CancellationToken ct)
    {
        var deployment = await FindDeploymentByLambdaId(lambdaId, ct);

        var update = new UpdateDefinitionBuilder<Deployment>()
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

    public async Task UpdateInstance(string cdpDeploymentId, string instanceId, DeploymentInstanceStatus instanceStatus,
        CancellationToken ct)
    {
        var fb = Builders<Deployment>.Filter;
        var filter = fb.And(
            fb.Eq(d => d.CdpDeploymentId, cdpDeploymentId));
        var update = Builders<Deployment>.Update.Set(d => d.Instances[instanceId], instanceStatus);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: ct);
    }

    public async Task<Deployment?> FindDeploymentByTaskArn(string taskArn, CancellationToken ct)
    {
        var fb = new FilterDefinitionBuilder<Deployment>();
        var filter = fb.Eq(d => d.TaskDefinitionArn, taskArn);
        var latestFirst = new SortDefinitionBuilder<Deployment>().Descending(d => d.Created);
        return await Collection.Find(filter).Sort(latestFirst).FirstOrDefaultAsync(ct);
    }

    public async Task<List<Deployment>> RunningDeploymentsForService(string[]? environments, string? service, string? team,
        string? user, string? status, CancellationToken ct)

    {
        var builder = Builders<Deployment>.Filter;
        var filter = builder.In(d => d.Status, [Running, Pending, Undeployed]);

        if (environments?.Length > 0)
        {
            var envFilter = builder.In(d => d.Environment, environments);
            filter &= envFilter;
        }

        if (!string.IsNullOrWhiteSpace(team))
        {
            var repos = await repositoryService.FindRepositoriesByTeamId(team, true, ct);
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
                Builders<Deployment>.Filter.Regex(d => d.Service, new BsonRegularExpression(service, "i"));
            filter &= partialServiceFilter;
        }

        var pipeline = new EmptyPipelineDefinition<Deployment>()
            .Match(filter)
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.Updated))
            .Group(d => new { d.Service, d.Environment }, grp => new { Root = grp.First() })
            .Project(grp => grp.Root);


        return await Collection.Aggregate(pipeline, cancellationToken: ct).ToListAsync(ct);
    }

    public async Task<List<Deployment>> RunningDeploymentsForService(string serviceName, CancellationToken ct)
    {
        var fb = new FilterDefinitionBuilder<Deployment>();
        var filter = fb.And(
            fb.Eq(d => d.Service, serviceName),
            fb.In(d => d.Status, [Running, Pending, Undeployed])
        );
        var pipeline = new EmptyPipelineDefinition<Deployment>()
            .Match(filter)
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.Updated))
            .Group(d => new { d.Environment }, grp => new { Root = grp.First() })
            .Project(grp => grp.Root);

        return await Collection.Aggregate(pipeline, cancellationToken: ct)
            .ToListAsync(ct);
    }

    public async Task<Paginated<Deployment>> FindLatest(string[]? favourites, string? environment,
        string? service, string? user,
        string? status,
        string? team,
        int offset = 0,
        int page = 0,
        int size = 0,
        CancellationToken ct = new()
    )
    {
        var builder = Builders<Deployment>.Filter;
        var filter = builder.Empty;

        if (!string.IsNullOrWhiteSpace(environment))
        {
            var environmentFilter = builder.Eq(d => d.Environment, environment);
            filter &= environmentFilter;
        }

        if (!string.IsNullOrWhiteSpace(team))
        {
            var repos = await repositoryService.FindRepositoriesByTeamId(team, true, ct);
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

        if (favourites?.Length > 0)
        {
            var repos = (await Task.WhenAll(favourites.Select(teamId =>
                    repositoryService.FindRepositoriesByTeamId(teamId, true, ct))))
                .SelectMany(r => r)
                .ToList();

            var servicesOwnedByTeam = repos.Select(r => r.Id);

            deployments = deployments.OrderByDescending(d => servicesOwnedByTeam.Contains(d.Service)).ToList();
        }

        var totalDeployments = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalDeployments / size));

        return new Paginated<Deployment>(deployments, page, size, totalPages);
    }

    public async Task<Paginated<DeploymentOrMigration>> FindLatestWithMigrations(
        string[]? favourites,
        string? environment,
        string? service,
        string? user,
        string? status,
        string? team,
        string? kind,
        int offset = 0,
        int page = 1,
        int size = 50,
        CancellationToken ct = new()
    )
    {
        var builder = Builders<Deployment>.Filter;
        var builderMigration = Builders<DatabaseMigration>.Filter;
        var filter = builder.Empty;
        var migrationFilter = builderMigration.Empty;

        if (!string.IsNullOrWhiteSpace(environment))
        {
            filter &= builder.Eq(d => d.Environment, environment);
            migrationFilter &= builderMigration.Eq(m => m.Environment, environment);
        }

        if (!string.IsNullOrWhiteSpace(team))
        {
            var repos = await repositoryService.FindRepositoriesByTeamId(team, true, ct);
            var servicesOwnedByTeam = repos.Select(r => r.Id).ToList();
            filter &= builder.In(d => d.Service, servicesOwnedByTeam);
            migrationFilter &= builderMigration.In(m => m.Service, servicesOwnedByTeam);
        }

        if (!string.IsNullOrWhiteSpace(service))
        {
            var regex = new BsonRegularExpression(service, "i");
            filter &= builder.Regex(d => d.Service, regex);
            migrationFilter &= builderMigration.Regex(m => m.Service, regex);
        }

        if (!string.IsNullOrWhiteSpace(user))
        {
            filter &= builder.Where(d =>
                (d.User != null && d.User.Id == user)
                || (d.User != null && d.User.DisplayName.ToLower().Contains(user.ToLower())));

            migrationFilter &= builderMigration.Where(m =>
                m.User.Id == user
                || (m.User.DisplayName != null && m.User.DisplayName.ToLower().Contains(user.ToLower())));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusLower = status.ToLower();
            filter &= builder.Where(d =>
                d.Status.ToLower() == statusLower || d.Status.ToLower().Contains(statusLower));

            migrationFilter &= builderMigration.Where(m =>
                m.Status.ToLower() == statusLower || m.Status.ToLower().Contains(statusLower));
        }

        switch (kind)
        {
            case Migration:
                filter = builder.Eq(m => m.CdpDeploymentId, null);
                break;
            case Deployment:
                migrationFilter = builderMigration.Eq(m => m.CdpMigrationId, null);
                break;
        }

        var migrationCollection = Collection.Database.GetCollection<DatabaseMigration>("migrations");
        var migrationPipeline = new EmptyPipelineDefinition<DatabaseMigration>()
            .Match(migrationFilter)
            .Project(m => new DeploymentOrMigration
            {
                Migration = m,
                Created = m.Created
            });

        var pipeline = new EmptyPipelineDefinition<Deployment>()
            .Match(filter)
            .Project(d => new DeploymentOrMigration { Deployment = d, Created = d.Created })
            .UnionWith(migrationCollection, migrationPipeline)
            .Sort(new SortDefinitionBuilder<DeploymentOrMigration>().Descending(d => d.Created))
            .Skip(offset + size * (page - DefaultPage))
            .Limit(size);

        var deployments = await Collection.Aggregate(pipeline).ToListAsync(ct);

        // TODO favourites should be on the entire result - "bring all favourite things to the front", not just on the current page
        //  we may be able to do this in an easier way in the UI
        if (favourites?.Length > 0)
        {
            var repos = (await Task.WhenAll(favourites.Select(teamId =>
                    repositoryService.FindRepositoriesByTeamId(teamId, true, ct))))
                .SelectMany(r => r)
                .ToList();

            var servicesOwnedByTeam = repos.Select(r => r.Id);
            deployments = deployments.OrderByDescending(d =>
                servicesOwnedByTeam.Contains(d.Deployment?.Service ?? d.Migration?.Service)
            ).ToList();
        }

        var totalDeployments = await Collection.CountDocumentsAsync(filter, cancellationToken: ct);

        var totalPages = Math.Max(1, (int)Math.Ceiling((double)totalDeployments / size));

        return new Paginated<DeploymentOrMigration>(deployments, page, size, totalPages);
    }

    public async Task<Deployment?> FindDeployment(string deploymentId, CancellationToken ct)
    {
        return await Collection.Find(d => d.CdpDeploymentId == deploymentId).FirstOrDefaultAsync(ct);
    }

    public async Task<DeploymentSettings?> FindDeploymentSettings(string service, string environment, CancellationToken ct)
    {
        var fb = new FilterDefinitionBuilder<Deployment>();
        var filter = fb.And(fb.Eq(d => d.Service, service), fb.Eq(d => d.Environment, environment));
        var sort = new SortDefinitionBuilder<Deployment>().Descending(d => d.Created);

        return await Collection
            .Find(filter)
            .Sort(sort)
            .Project(d => new DeploymentSettings { Cpu = d.Cpu, Memory = d.Memory, InstanceCount = d.InstanceCount })
            .FirstOrDefaultAsync(ct);
    }

    public async Task UpdateOverallTaskStatus(Deployment deployment, CancellationToken ct)
    {
        var update = Builders<Deployment>.Update
            .Set(d => d.Status, deployment.Status)
            .Set(d => d.Unstable, deployment.Unstable)
            .Set(d => d.Updated, deployment.Updated)
            .Set(d => d.TaskDefinitionArn, deployment.TaskDefinitionArn)
            .Set(d => d.FailureReasons, deployment.FailureReasons);

        await Collection.FindOneAndUpdateAsync(d => d.Id == deployment.Id, update, cancellationToken: ct);
    }

    private async Task<List<UserDetails>> UserDetailsList(CancellationToken ct)
    {
        var userFilter = Builders<Deployment>.Filter.And(
            Builders<Deployment>.Filter.Ne(d => d.User, null),
            Builders<Deployment>.Filter.Nin(d => d.User!.DisplayName, _excludedDisplayNames)
        );

        var users = await Collection
            .Distinct<Deployment, UserDetails>(d => d.User!, userFilter, cancellationToken: ct)
            .ToListAsync(ct);

        users.Sort((a, b) => string.Compare(a.DisplayName, b.DisplayName, StringComparison.OrdinalIgnoreCase));

        return users;
    }

    public async Task<DeploymentFilters> GetDeploymentsFilters(CancellationToken ct)
    {
        var serviceNames = await Collection
            .Distinct(d => d.Service, FilterDefinition<Deployment>.Empty, cancellationToken: ct)
            .ToListAsync(ct);

        var deploymentStatusesTask = Collection
            .Distinct(d => d.Status, FilterDefinition<Deployment>.Empty, cancellationToken: ct)
            .ToListAsync(ct);

        var migrationStatusesTask = Collection.Database
            .GetCollection<DatabaseMigration>("migrations")
            .Distinct(m => m.Status, FilterDefinition<DatabaseMigration>.Empty, cancellationToken: ct)
            .ToListAsync(ct);

        await Task.WhenAll(deploymentStatusesTask, migrationStatusesTask);

        var allStatuses = new HashSet<string>(
            deploymentStatusesTask.Result
                .Concat(migrationStatusesTask.Result)
                .Where(s => !string.IsNullOrWhiteSpace(s)),
            StringComparer.OrdinalIgnoreCase
        );

        var statuses = allStatuses.OrderBy(s => s).ToList();

        var users = await UserDetailsList(ct);

        var kinds = new List<string> { Deployment, Migration };

        serviceNames.Sort();

        return new DeploymentFilters { Services = serviceNames, Users = users, Statuses = statuses, Kinds = kinds };
    }

    public async Task<DeploymentFilters> GetWhatsRunningWhereFilters(CancellationToken ct)
    {
        var builder = Builders<Deployment>.Filter;
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