using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Models;
using Defra.Cdp.Backend.Api.Utils;
using MongoDB.Driver;
using Team = Defra.Cdp.Backend.Api.Services.Entities.Model.Team;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Services.Entities;

public interface IEntitiesService
{
    Task<Entity?> GetEntity(string entityName, CancellationToken cancellationToken);
    Task<List<Entity>> GetEntities(EntityMatcher matcher, CancellationToken cancellationToken); //Used for tests
    Task<List<Entity>> GetEntities(EntityMatcher matcher, EntitySearchOptions options, CancellationToken cancellationToken);
    Task<List<Entity>> GetCreatingEntities(CancellationToken cancellationToken);
    Task<List<Entity>> EntitiesPendingDecommission(CancellationToken cancellationToken);

    Task<EntitiesService.EntityFilters>
        GetFilters(string[] teamIds, Type[] types, Status[] statuses, CancellationToken cancellationToken);

    Task Create(Entity entity, CancellationToken cancellationToken);
    Task AddTag(string entityName, string tag, CancellationToken cancellationToken);
    Task RemoveTag(string entityName, string tag, CancellationToken cancellationToken);

    Task SetDecommissionDetail(string entityName, string userId, string userDisplayName,
        CancellationToken cancellationToken);

    Task DecommissioningWorkflowTriggered(string entityName, CancellationToken cancellationToken);

    Task UpdateEnvironmentState(PlatformStatePayload state, Dictionary<string, UserServiceTeam> userServiceTeams,
        CancellationToken cancellationToken);

    Task BulkUpdateEntityStatus(CancellationToken cancellationToken);
}

public class EntitiesService(
    IMongoDbClientFactory connectionFactory,
    ILoggerFactory loggerFactory)
    : MongoService<Entity>(connectionFactory, CollectionName, loggerFactory), IEntitiesService
{
    private const string CollectionName = "entities";
    private readonly ILogger<EntitiesService> _logger = loggerFactory.CreateLogger<EntitiesService>();

    protected override List<CreateIndexModel<Entity>> DefineIndexes(
        IndexKeysDefinitionBuilder<Entity> builder)
    {
        return
        [
            new CreateIndexModel<Entity>(builder.Ascending(s => s.Name), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<Entity>(builder.Ascending(s => s.Status), new CreateIndexOptions()),
            new CreateIndexModel<Entity>(builder.Ascending(s => s.Type), new CreateIndexOptions()),
            new CreateIndexModel<Entity>(Builders<Entity>.IndexKeys.Ascending("teams.teamId"), new CreateIndexOptions())
        ];
    }

    /// <summary>
    /// Gets a single entity by name.
    /// </summary>
    /// <param name="entityName"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<Entity?> GetEntity(string entityName, CancellationToken cancellationToken)
    {
        var entity = await Collection.Find(e => e.Name == entityName)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);

        entity?.CalculateOverallProgress();

        return entity;
    }

    /// <summary>
    /// Returns a list of all entities that match the matcher with default options.
    /// </summary>
    /// <param name="matcher"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<Entity>> GetEntities(EntityMatcher matcher, CancellationToken cancellationToken)
    {
        return await GetEntities(matcher, new EntitySearchOptions(), cancellationToken);
    }

    /// <summary>
    /// Returns a list of all entities that match the matcher.
    /// Performance Note: unfiltered the payload will be quite large, consider setting `options.Summary` to reduce the
    /// size of the payload. 
    /// </summary>
    /// <param name="matcher"></param>
    /// <param name="options"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<List<Entity>> GetEntities(EntityMatcher matcher, EntitySearchOptions options,
        CancellationToken cancellationToken)
    {
        var sortBuilder = Builders<Entity>.Sort;
        var projectionBuilder = Builders<Entity>.Projection;

        var sortDefinition = options.SortBy == EntitySortBy.Team
            ? sortBuilder.Ascending(e => e.Teams)
            : sortBuilder.Ascending(e => e.Name);

        ProjectionDefinition<Entity, Entity> projectDefinition = projectionBuilder.Exclude(e => e.Id);
        if (options.Summary)
        {
            projectDefinition = projectionBuilder.Combine(
                projectionBuilder.Exclude(e => e.Id),
                projectionBuilder.Exclude(e => e.Environments));
        }

        return await Collection
            .Find(matcher.Match())
            .Sort(sortDefinition)
            .Project(projectDefinition)
            .ToListAsync(cancellationToken);
    }

    /// <summary>
    /// Original version used an aggregation to perform this in a single pass.
    /// Performance wise there's not a massive difference between doing it as a find/project
    /// and grouping in code, while improving readability.
    /// </summary>
    /// <param name="teamIds"></param>
    /// <param name="types"></param>
    /// <param name="statuses"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    public async Task<EntityFilters> GetFilters(string[] teamIds, Type[] types, Status[] statuses,
        CancellationToken cancellationToken)
    {
        var matcher = new EntityMatcher { Types = types, Statuses = statuses, TeamIds = teamIds };
        var pb = Builders<Entity>.Projection;
        var projection = pb.Combine(
            pb.Include(e => e.Name),
            pb.Include(e => e.Teams),
            pb.Exclude(e => e.Id)
        );

        var results = await Collection
            .Find(matcher.Match())
            .Project<EntityFiltersProjection>(projection)
            .ToListAsync(cancellationToken);

        var names = results
            .Select(doc => doc.Name)
            .Distinct()
            .ToList();

        var teams = results
            .SelectMany(doc => doc.Teams ?? [])
            .DistinctBy(t => t.TeamId)
            .ToList();

        names.Sort();
        teams.Sort((a, b) => string.Compare(a.TeamId, b.TeamId, StringComparison.OrdinalIgnoreCase));

        return new EntityFilters { Teams = teams, Entities = names };
    }

    private class EntityFiltersProjection
    {
        public required string Name { get; set; }
        public IEnumerable<Team>? Teams { get; set; }
    }

    public class EntityFilters
    {
        [JsonPropertyName("entities")] public List<string> Entities { get; init; } = [];
        [JsonPropertyName("teams")] public List<Team> Teams { get; init; } = [];
    }

    public async Task Create(Entity entity, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
    }

    public async Task SetDecommissionDetail(string entityName, string userId, string userDisplayName,
        CancellationToken cancellationToken)
    {
        var filter = Builders<Entity>.Filter.Eq(entity => entity.Name, entityName);
        var update = Builders<Entity>.Update.Set(e => e.Decommissioned,
            new Decommission
            {
                DecommissionedBy = new UserDetails { Id = userId, DisplayName = userDisplayName },
                Started = DateTime.UtcNow,
                Finished = null,
                WorkflowsTriggered = false
            })
            .Set(e => e.Status, Status.Decommissioning);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task<List<Entity>> GetCreatingEntities(CancellationToken cancellationToken)
    {
        return await GetEntities(new EntityMatcher { Status = Status.Creating },
            new EntitySearchOptions { Summary = true }, cancellationToken);
    }

    public async Task<List<Entity>> EntitiesPendingDecommission(CancellationToken cancellationToken)
    {
        return await GetEntities(new EntityMatcher { Status = Status.Decommissioning },
            new EntitySearchOptions { Summary = true }, cancellationToken);
    }

    public async Task AddTag(string entityName, string tag, CancellationToken cancellationToken)
    {
        var update = new UpdateDefinitionBuilder<Entity>().AddToSet(e => e.Tags, tag);
        await Collection.UpdateOneAsync(e => e.Name == entityName, update, new UpdateOptions(), cancellationToken);
    }

    public async Task RemoveTag(string entityName, string tag, CancellationToken cancellationToken)
    {
        var update = new UpdateDefinitionBuilder<Entity>().Pull(e => e.Tags, tag);
        await Collection.UpdateOneAsync(e => e.Name == entityName, update, new UpdateOptions(), cancellationToken);
    }

    public async Task DecommissioningWorkflowTriggered(string entityName, CancellationToken cancellationToken)
    {
        var filter = Builders<Entity>.Filter.Eq(entity => entity.Name, entityName);
        var update = Builders<Entity>.Update.Set(e => e.Decommissioned!.WorkflowsTriggered, true);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    /// <summary>
    /// Updates the status of all entities based on their 'Progress' data for each env.
    /// Takes into account decommissioned services as well. 
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task BulkUpdateEntityStatus(CancellationToken cancellationToken)
    {
        var fb = new FilterDefinitionBuilder<Entity>();
        var ub = new UpdateDefinitionBuilder<Entity>();

        var hasDecommissionStarted = fb.Exists(e => e.Decommissioned!.Started);
        var hasDecommissionNotStarted = fb.Not(hasDecommissionStarted);

        // We do not expect the service to be provisioned in infra-dev
        var environmentsToCheck = CdpEnvironments.EnvironmentExcludingInfraDev;

        var isCompleteInAllEnvs =
            fb.And(environmentsToCheck.Select(env => fb.Eq(e => e.Progress[env].Complete, true)));

        // We DO expect it to be removed from infra-dev.
        var isRemovedFromAllEnvs = fb.And(
            environmentsToCheck.Select(env => fb.Exists(e => e.Progress[env], false))
        );

        // Filters to find records that should change state.
        // The filters exclude anything already in the target state to reduce the number of redundant updates.
        var shouldBeSetToCreated = fb.And(
            fb.Ne(e => e.Status, Status.Created), 
            hasDecommissionNotStarted,
            isCompleteInAllEnvs
        );

        var shouldBeSetToCreating = fb.And(
            fb.Ne(e => e.Status, Status.Creating),
            fb.Ne(e => e.Status, Status.Created),
            hasDecommissionNotStarted,
            fb.Not(isCompleteInAllEnvs)
        );

        var shouldBeSetToDecommissioned = fb.And(
            fb.Ne(e => e.Status, Status.Decommissioned),
            hasDecommissionStarted,
            isRemovedFromAllEnvs
        );

        var shouldBeSetToDecommissioning = fb.And(
            fb.Ne(e => e.Status, Status.Decommissioning),
            fb.Ne(e => e.Status, Status.Decommissioned),
            hasDecommissionStarted,
            fb.Not(isRemovedFromAllEnvs)
        );

        // Bulk update all the statuses that need to change
        var models = new List<WriteModel<Entity>>
        {
            new UpdateManyModel<Entity>(shouldBeSetToCreated, ub.Set(e => e.Status, Status.Created)),
            new UpdateManyModel<Entity>(shouldBeSetToCreating, ub.Set(e => e.Status, Status.Creating)),
            new UpdateManyModel<Entity>(shouldBeSetToDecommissioning, ub.Set(e => e.Status, Status.Decommissioning)),
            new UpdateManyModel<Entity>(shouldBeSetToDecommissioned, ub
                .Set(e => e.Status, Status.Decommissioned)
                .Set(e => e.Decommissioned!.Finished, DateTime.UtcNow)
                .Unset(e => e.Metadata)
            )
        };

        var result = await Collection
            .WithReadPreference(ReadPreference.Primary)
            .WithWriteConcern(WriteConcern.WMajority)
            .WithReadConcern(ReadConcern.Majority)
            .BulkWriteAsync(models, cancellationToken: cancellationToken);
        _logger.LogInformation("Updated status for {Updated} entities", result.ModifiedCount);
    }

    /// <summary>
    /// Updates entities based on a PlatformStatePayload for a given environment.
    /// When the entity does not exist it will create the entity using the data available.
    /// </summary>
    /// <param name="state"></param>
    /// <param name="userServiceTeams"></param>
    /// <param name="cancellationToken"></param>
    public async Task UpdateEnvironmentState(PlatformStatePayload state,
        Dictionary<string, UserServiceTeam> userServiceTeams, CancellationToken cancellationToken)
    {
        var env = state.Environment;
        var models = new List<WriteModel<Entity>>();

        foreach (var kv in state.Tenants)
        {
            var filter = Builders<Entity>.Filter.Eq(f => f.Name, kv.Key);
            var update = Builders<Entity>.Update.Set(e => e.Name, kv.Key)
                .Set(e => e.Environments[env], kv.Value.Tenant)
                .Set(e => e.Progress[env], kv.Value.Progress);

            if (kv.Value.Metadata != null)
            {
                update = update.Set(e => e.Metadata, kv.Value.Metadata);
                if (!string.IsNullOrEmpty(kv.Value.Metadata?.Type) &&
                    Enum.TryParse<Type>(kv.Value.Metadata?.Type, true, out var entityType))
                {
                    update = update.Set(e => e.Type, entityType);
                }

                if (!string.IsNullOrEmpty(kv.Value.Metadata?.Subtype) &&
                    Enum.TryParse<SubType>(kv.Value.Metadata?.Subtype, true, out var entitySubType))
                {
                    update = update.Set(e => e.SubType, entitySubType);
                }

                if (!string.IsNullOrEmpty(kv.Value.Metadata?.Created))
                {
                    update = DateTime.TryParse(kv.Value.Metadata?.Created, out var created)
                        ? update.SetOnInsert(e => e.Created, created)
                        : update.SetOnInsert(e => e.Created, DateTime.UtcNow);
                }

                var teams = kv.Value.Metadata?.Teams?
                    .Where(t => t != null!)
                    .Select(t =>
                        userServiceTeams.TryGetValue(t, out var userServiceTeam)
                            ? new Team { TeamId = t, Name = userServiceTeam.name }
                            : new Team { TeamId = t, Name = t })
                    .ToList() ?? [];
                update = update.Set(e => e.Teams, teams);
            }

            // A small number of tenants aren't defined in all environments (defined in metadata.Environments)
            // To ensure the status is calculated correctly (and to keep the calculation simple) we just set the
            // completed status to 'true' in the envs these services aren't created in.
            if (kv.Value.Metadata?.Environments != null)
            {
                var restrictedEnvs = kv.Value.Metadata.Environments;
                var envsToSkip = CdpEnvironments.EnvironmentExcludingInfraDev.Where(e => !restrictedEnvs.Contains(e))
                    .ToArray();
                update = envsToSkip.Aggregate(update,
                    (current, envToSkip) => current.Set(e => e.Progress[envToSkip].Complete, true));
            }

            models.Add(new UpdateOneModel<Entity>(filter, update) { IsUpsert = true });
        }

        // Remove environment for any service not in the list.
        var fb = Builders<Entity>.Filter;
        var removeMissing = new UpdateManyModel<Entity>(
            fb.And(
                fb.Nin(f => f.Name, state.Tenants.Keys),
                fb.Exists(f => f.Environments[env])),
            Builders<Entity>.Update
                .Unset(e => e.Environments[env])
                .Unset(e => e.Progress[env])
        )
        { IsUpsert = false };

        models.Add(removeMissing);

        if (models.Count > 0)
        {
            var result = await Collection
                .WithReadPreference(ReadPreference.Primary)
                .WithWriteConcern(WriteConcern.WMajority)
                .WithReadConcern(ReadConcern.Majority)
                .BulkWriteAsync(models, cancellationToken: cancellationToken);
            _logger.LogInformation("Updated {Updated}, inserted {Inserted}, removed {Removed} tenants in {Env}",
                result.ModifiedCount, result.InsertedCount, result.DeletedCount, env);
        }
    }
}