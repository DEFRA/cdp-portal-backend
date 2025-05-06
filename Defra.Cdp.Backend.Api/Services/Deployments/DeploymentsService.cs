using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.AutoDeploymentTriggers;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;
using Defra.Cdp.Backend.Api.Services.Migrations;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;
using static Defra.Cdp.Backend.Api.Services.Aws.Deployments.DeploymentStatus;

namespace Defra.Cdp.Backend.Api.Services.Deployments;

public interface IDeploymentsService
{
    Task                RegisterDeployment(Deployment deployment, CancellationToken ct);
    Task<bool>          LinkDeployment(string cdpId, string lambdaId, CancellationToken ct);
    Task                UpdateDeployment(Deployment deployment, CancellationToken ct);
    Task<Deployment?> FindDeploymentByLambdaId(string lambdaId, CancellationToken ct);
    Task<bool>          UpdateDeploymentStatus(string lambdaId, string eventName, string reason, CancellationToken ct);
    
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

    Task<List<Deployment>> FindWhatsRunningWhere(string[]? environments, string? service, string? team,
        string? user, string? status, CancellationToken ct);
    Task<List<Deployment>> FindWhatsRunningWhere(string serviceName, CancellationToken ct);
    Task<DeploymentFilters> GetWhatsRunningWhereFilters(CancellationToken ct);
    Task<DeploymentFilters> GetDeploymentsFilters(CancellationToken ct);
    Task<DeploymentSettings?> FindDeploymentSettings(string service, string environment, CancellationToken ct);
}

public class DeploymentsService : MongoService<Deployment>, IDeploymentsService
{
    public static readonly int DefaultPageSize = 50;
    public static readonly int DefaultPage = 1;
    private const string Migration = "migration";
    private const string Deployment = "deployment";
    private readonly IRepositoryService _repositoryService;
    private readonly IUserServiceFetcher _userServiceFetcher;
    private readonly HashSet<string> _excludedDisplayNames =
        new(StringComparer.CurrentCultureIgnoreCase) { "n/a", "admin", "GitHub Workflow" };
    
    public DeploymentsService(
        IMongoDbClientFactory connectionFactory, 
        IRepositoryService repositoryService, 
        IUserServiceFetcher userServiceFetcher, 
        ILoggerFactory loggerFactory) : base(connectionFactory, "deploymentsV2", loggerFactory)
    {
        _repositoryService = repositoryService;
        _userServiceFetcher = userServiceFetcher;
    }

    protected override List<CreateIndexModel<Deployment>> DefineIndexes(IndexKeysDefinitionBuilder<Deployment> builder)
    {
        var created           = new CreateIndexModel<Deployment>(builder.Descending(d => d.Created));
        var updated           = new CreateIndexModel<Deployment>(builder.Descending(d => d.Updated));
        var lambdaId          = new CreateIndexModel<Deployment>(builder.Descending(d => d.LambdaId));
        var cdpDeploymentId   = new CreateIndexModel<Deployment>(builder.Descending(d => d.CdpDeploymentId));
        var envServiceVersion = new CreateIndexModel<Deployment>(builder.Combine(
            builder.Descending(d => d.Environment),
            builder.Descending(d => d.Service),
            builder.Descending(d => d.Version)
        ));
        
        return [created, updated, lambdaId, cdpDeploymentId, envServiceVersion];
    }

    public async Task RegisterDeployment(Deployment deployment, CancellationToken ct)
    {
        await Collection.InsertOneAsync(await WithAuditData(deployment, ct), null, ct);
    }

    private async Task<Deployment> WithAuditData(Deployment deployment, CancellationToken ct)
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
            Logger.LogError("Failed to lookup teams for {Services}, {Ex}", deployment.Service, ex);
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
                Logger.LogError("Failed to lookup user for {UserId}, {Ex}", deployment.User?.Id, ex);
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

    public async Task<Deployment?>  FindDeploymentByTaskArn(string taskArn, CancellationToken ct)
    {
        var fb = new FilterDefinitionBuilder<Deployment>();
        var filter = fb.Eq(d => d.TaskDefinitionArn, taskArn);
        var latestFirst = new SortDefinitionBuilder<Deployment>().Descending(d => d.Created);
        return await Collection.Find(filter).Sort(latestFirst).FirstOrDefaultAsync(ct);
    }

    public async Task<List<Deployment>> FindWhatsRunningWhere(string[]? environments, string? service, string? team,
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
                Builders<Deployment>.Filter.Regex(d => d.Service,  new BsonRegularExpression(service, "i"));
            filter &= partialServiceFilter;
        }
        
        var pipeline = new EmptyPipelineDefinition<Deployment>()
            .Match(filter)
            .Sort(new SortDefinitionBuilder<Deployment>().Descending(d => d.Updated))
            .Group(d => new { d.Service, d.Environment }, grp => new { Root = grp.First() })
            .Project(grp => grp.Root);
            
        
        return await Collection.Aggregate(pipeline, cancellationToken: ct).ToListAsync(ct);
    }

    public async Task<List<Deployment>> FindWhatsRunningWhere(string serviceName, CancellationToken ct)
    {
        var fb = new FilterDefinitionBuilder<Deployment>();
        var filter = fb.And(
            fb.Eq(d => d.Service, serviceName),
            fb.In(d => d.Status, new[] { Running, Pending, Undeployed })
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

        if (favourites?.Length > 0)
        {
            var repos = (await Task.WhenAll(favourites.Select(teamId =>
                    _repositoryService.FindRepositoriesByTeamId(teamId, true, ct))))
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
            var repos = await _repositoryService.FindRepositoriesByTeamId(team, true, ct);
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
                d.User != null && (d.User.Id == user || d.User.DisplayName.ToLower().Contains(user.ToLower())));
            migrationFilter &= builderMigration.Where(m =>
                m.User.Id == user ||
                (m.User.DisplayName != null && m.User.DisplayName.ToLower().Contains(user.ToLower())));
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            var statusLower = status.ToLower();
            filter &= builder.Where(d => d.Status.ToLower().Contains(statusLower));
            migrationFilter &= builderMigration.Where(m => m.Status.ToLower().Contains(statusLower));
        }

        if (kind == nameof(Migration))
            filter = builder.Eq(d => d.CdpDeploymentId, null);
        else if (kind == nameof(Deployment))
            migrationFilter = builderMigration.Eq(m => m.CdpMigrationId, null);

        var favouriteServiceIds = new HashSet<string>();
        if (favourites?.Length > 0)
        {
            var repos = (await Task.WhenAll(favourites.Select(teamId =>
                    _repositoryService.FindRepositoriesByTeamId(teamId, true, ct))))
                .SelectMany(r => r)
                .Select(r => r.Id)
                .ToHashSet();
            favouriteServiceIds = repos;
        }

        var favouriteArray = new BsonArray(favouriteServiceIds);

        var deploymentStages = BuildAggregationStages(filter, "Deployment", favouriteArray);
        var migrationStages = BuildAggregationStages(migrationFilter, "Migration", favouriteArray);

        var pipeline = new List<BsonDocument>();
        pipeline.AddRange(deploymentStages);
        pipeline.Add(new BsonDocument("$unionWith",
            new BsonDocument { { "coll", "migrations" }, { "pipeline", new BsonArray(migrationStages) } }));
        pipeline.Add(new BsonDocument("$sort", new BsonDocument { { "isFavourite", -1 }, { "Created", -1 } }));
        pipeline.Add(new BsonDocument("$facet",
            new BsonDocument
            {
                {
                    "items",
                    new BsonArray
                    {
                        new BsonDocument("$skip", offset + size * (page - 1)), new BsonDocument("$limit", size)
                    }
                },
                { "total", new BsonArray { new BsonDocument("$count", "count") } }
            }));

        var result = await Collection.Aggregate<BsonDocument>(pipeline).FirstOrDefaultAsync(ct);

        var rawItems = result["items"].AsBsonArray;
        var total = result["total"].AsBsonArray.FirstOrDefault()?["count"].AsInt32 ?? 0;

        var deployments = rawItems.Select(doc => new DeploymentOrMigration
        {
            Deployment =
                doc["Deployment"].IsBsonNull
                    ? null
                    : BsonSerializer.Deserialize<Deployment>(doc["Deployment"].AsBsonDocument),
            Migration =
                doc["Migration"].IsBsonNull
                    ? null
                    : BsonSerializer.Deserialize<DatabaseMigration>(doc["Migration"].AsBsonDocument),
            Created = doc["Created"].ToUniversalTime(),
            IsFavourite = doc["isFavourite"].ToBoolean()
        }).ToList();

        var totalPages = Math.Max(1, (int)Math.Ceiling((double)total / size));

        return new Paginated<DeploymentOrMigration>(deployments, page, size, totalPages);
    }

    private static List<BsonDocument> BuildAggregationStages<T>(
        FilterDefinition<T> filter,
        string rootType,
        BsonArray favouriteArray)
    {
        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<T>();
        var renderedFilter = filter.Render(serializer, BsonSerializer.SerializerRegistry);

        return
        [
            new("$match", renderedFilter),
            new("$addFields", new BsonDocument("isFavourite",
                new BsonDocument("$in", new BsonArray { "$service", favouriteArray }))),
            new BsonDocument("$project",
                new BsonDocument
                {
                    { "Deployment", rootType == "Deployment" ? "$$ROOT" : BsonNull.Value },
                    { "Migration", rootType == "Migration" ? "$$ROOT" : BsonNull.Value },
                    { "Created", "$created" },
                    { "isFavourite", "$isFavourite" }
                })
        ];
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

    public async Task UpdateDeployment(Deployment deployment, CancellationToken ct)
    {
        await Collection.ReplaceOneAsync(d => d.Id == deployment.Id, deployment, cancellationToken: ct);
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