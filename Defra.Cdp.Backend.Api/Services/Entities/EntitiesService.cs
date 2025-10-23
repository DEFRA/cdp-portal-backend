using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Models;
using Defra.Cdp.Backend.Api.Utils;
using MongoDB.Bson;
using MongoDB.Driver;
using Team = Defra.Cdp.Backend.Api.Services.Entities.Model.Team;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Services.Entities;

public interface IEntitiesService
{
    Task<List<Entity>> GetEntities(EntityMatcher matcher, CancellationToken cancellationToken);
Task<List<Entity>> GetEntitiesWithoutEnvState(EntityMatcher matcher, CancellationToken cancellationToken);
    Task<EntitiesService.EntityFilters>
        GetFilters(Type[] types, Status[] statuses, CancellationToken cancellationToken);

    Task Create(Entity entity, CancellationToken cancellationToken);
    Task UpdateStatus(Status overallStatus, string entityName, CancellationToken cancellationToken);

    Task SetDecommissionDetail(string entityName, string userId, string userDisplayName,
        CancellationToken cancellationToken);

    Task<Entity?> GetEntity(string entityName, CancellationToken cancellationToken);
    Task<List<Entity>> GetCreatingEntities(CancellationToken cancellationToken);

    Task AddTag(string entityName, string tag, CancellationToken cancellationToken);
    Task RemoveTag(string entityName, string tag, CancellationToken cancellationToken);
    Task RefreshTeams(List<Repository> repos, CancellationToken cancellationToken);
    Task<List<Entity>> EntitiesPendingDecommission(CancellationToken cancellationToken);
    Task DecommissioningWorkflowTriggered(string entityName, CancellationToken cancellationToken);
    Task DecommissionFinished(string entityName, CancellationToken contextCancellationToken);

    Task UpdateEnvironmentState(PlatformStatePayload state, CancellationToken cancellationToken);
    Task BulkUpdateCreationStatus(CancellationToken cancellationToken);
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
        return [
            new CreateIndexModel<Entity>(builder.Ascending(s => s.Name), new CreateIndexOptions { Unique = true }),
            new CreateIndexModel<Entity>(builder.Ascending(s => s.Status), new CreateIndexOptions()),
            new CreateIndexModel<Entity>(builder.Ascending(s => s.Type), new CreateIndexOptions())
        ];
    }

    public async Task<List<Entity>> GetEntitiesWithoutEnvState(EntityMatcher matcher, CancellationToken cancellationToken)
    {
        var pb = Builders<Entity>.Projection;
        var removeEnvs = pb.Combine(pb.Exclude(e => e.Envs), pb.Exclude(e => e.Progress));
        return await Collection.Find(matcher.Match()).Project<Entity>(removeEnvs).ToListAsync(cancellationToken);        
    }
    
    public async Task<List<Entity>> GetEntities(EntityMatcher matcher, CancellationToken cancellationToken)
    {
        return await Collection.Find(matcher.Match()).ToListAsync(cancellationToken);
    }

    public async Task<EntityFilters> GetFilters(Type[] types, Status[] statuses, CancellationToken cancellationToken)
    {
        var filters = new Dictionary<string, BsonDocument>();

        if (types.Length > 0)
        {
            var typeValues = new BsonArray(types.Select(t => t.ToString()));
            filters["type"] = new BsonDocument("$in", typeValues);
        }

        if (statuses.Length > 0)
        {
            var statusValues = new BsonArray(statuses.Select(s => s.ToString()));
            filters["status"] = new BsonDocument("$in", statusValues);
        }

        var pipeline = new[]
        {
            new BsonDocument("$match",
                new BsonDocument
                {
                    filters
                }),
            new BsonDocument("$facet",
                new BsonDocument
                {
                    {
                        "uniqueNames",
                        new BsonArray
                        {
                            new BsonDocument("$group", new BsonDocument { { "_id", "$name" } }),
                            new BsonDocument("$project", new BsonDocument { { "_id", 0 }, { "name", "$_id" } })
                        }
                    },
                    {
                        "uniqueTeams", new BsonArray
                        {
                            new BsonDocument("$unwind", "$teams"),
                            new BsonDocument("$group",
                                new BsonDocument
                                {
                                    {
                                        "_id",
                                        new BsonDocument
                                            {
                                                { "teamId", "$teams.teamId" }, { "teamName", "$teams.name" }
                                            }
                                    }
                                }),
                            new BsonDocument("$project",
                                new BsonDocument
                                {
                                    { "_id", 0 }, { "teamId", "$_id.teamId" }, { "teamName", "$_id.teamName" }
                                })
                        }
                    }
                })
        };
        
        var result = await Collection.Aggregate<BsonDocument>(pipeline, cancellationToken: cancellationToken)
            .FirstAsync(cancellationToken);

        return new EntityFilters
        {
            Entities = result["uniqueNames"].AsBsonArray
                .Select(n => n["name"].AsString)
                .ToList(),
            Teams = result["uniqueTeams"].AsBsonArray
                .Select(t => new Team { TeamId = t["teamId"].AsString, Name = t["teamName"].AsString })
                .ToList()
        };
    }

    public async Task Create(Entity entity, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
    }

    [Obsolete("Now handled by BulkUpdateCreationStatus")]
    public async Task UpdateStatus(Status overallStatus, string entityName, CancellationToken cancellationToken)
    {
        var filter = Builders<Entity>.Filter.Eq(entity => entity.Name, entityName);
        var update = Builders<Entity>.Update.Set(e => e.Status, overallStatus);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
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
            });
        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task<Entity?> GetEntity(string entityName, CancellationToken cancellationToken)
    {
        return await Collection.Find(e => e.Name == entityName)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public async Task<List<Entity>> GetCreatingEntities(CancellationToken cancellationToken)
    {
        return await GetEntities(new EntityMatcher { Status = Status.Creating }, cancellationToken);
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

    [Obsolete("will be replaced in CORE-1614")]
    public async Task RefreshTeams(List<Repository> repos, CancellationToken cancellationToken)
    {
        var updates = repos.Select(r =>
        {
            var entity = r.Id;
            var teams = r.Teams.Select(t => new Team { TeamId = t.TeamId, Name = t.Name }).ToList();

            var filterBuilder = Builders<Entity>.Filter;
            var filter = filterBuilder.Eq(e => e.Name, entity);

            var updateBuilder = Builders<Entity>.Update;
            var update = updateBuilder.Set(e => e.Teams, teams);
            return new UpdateManyModel<Entity>(filter, update) { IsUpsert = false };
        }).ToList();

        await Collection.BulkWriteAsync(updates, new BulkWriteOptions(), cancellationToken);
    }

    public async Task<List<Entity>> EntitiesPendingDecommission(CancellationToken cancellationToken)
    {
        return await GetEntitiesWithoutEnvState(new EntityMatcher { Status = Status.Decommissioning }, cancellationToken);
    }

    public async Task DecommissioningWorkflowTriggered(string entityName, CancellationToken cancellationToken)
    {
        var filter = Builders<Entity>.Filter.Eq(entity => entity.Name, entityName);
        var update = Builders<Entity>.Update.Set(e => e.Decommissioned!.WorkflowsTriggered, true);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    [Obsolete("Now handled by BulkUpdateCreationStatus")]
    public async Task DecommissionFinished(string entityName, CancellationToken cancellationToken)
    {
        var filter = Builders<Entity>.Filter.Eq(entity => entity.Name, entityName);
        var update = Builders<Entity>.Update.Combine(
            Builders<Entity>.Update.Set(e => e.Decommissioned!.Finished, DateTime.UtcNow),
            Builders<Entity>.Update.Set(e => e.Status, Status.Decommissioned));
        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public class EntityFilters
    {
        public List<string> Entities { get; set; } = [];
        public List<Team> Teams { get; set; } = [];
    }

    /// <summary>
    /// Brings all the creation Status fields into line based off the data from the state lambda.
    /// Consists of 4 updates that check:
    /// - its current state (to minimize redundant updates if its already correct)
    /// - if its decomissioned (checks presense of the decom section)
    /// - if the complete flag for each environment is true or not
    /// </summary>
    /// <param name="cancellationToken"></param>
    public async Task BulkUpdateCreationStatus(CancellationToken cancellationToken)
    {
        var fb = new FilterDefinitionBuilder<Entity>();
        var ub = new UpdateDefinitionBuilder<Entity>();
        
        var isDecommissioned = fb.Eq(e => e.Decommissioned!.WorkflowsTriggered, true);
        var isNotDecommissioned = fb.Not(isDecommissioned);
        var areAllEnvsComplete =
            fb.And(CdpEnvironments.EnvironmentIds.Select(env => fb.Eq(e => e.Progress[env].Complete, true)));
        
        // The environment (and thus status
        var isRemovedFromAllEnvs = fb.And(
            CdpEnvironments.EnvironmentIds.Select(env => fb.Exists(e => e.Progress[env], false))
        );
        
        var hasComplete = fb.And(
            fb.Ne(e => e.Status, Status.Created),
            isNotDecommissioned,
            areAllEnvsComplete
        );
        
        var isInProgress = fb.And(
            fb.Ne(e => e.Status, Status.Creating),
            isNotDecommissioned,
            fb.Not(areAllEnvsComplete)
        );

        var hasBeenDecommissioned = fb.And(
            fb.Ne(e => e.Status, Status.Decommissioned),
            isDecommissioned,
            isRemovedFromAllEnvs 
        );
        
        var isDecommissioning = fb.And(
            fb.Ne(e => e.Status, Status.Decommissioning),
            isDecommissioned,
            fb.Not(isRemovedFromAllEnvs) 
        );
        
        // Bulk update all the statuses that need to change
        var models = new List<WriteModel<Entity>> {
            new UpdateManyModel<Entity>(hasComplete, ub.Set(e => e.Status, Status.Created)),
            new UpdateManyModel<Entity>(isInProgress, ub.Set(e => e.Status, Status.Creating)),
            new UpdateManyModel<Entity>(isDecommissioning, ub.Set(e => e.Status, Status.Decommissioning)),
            new UpdateManyModel<Entity>(hasBeenDecommissioned, ub
                .Set(e => e.Status, Status.Decommissioned)
                .Set(e => e.Decommissioned!.Finished, DateTime.UtcNow)
                .Set(e => e.Status, Status.Decommissioned)
                .Unset(e => e.Metadata)
            )
        };
        var result = await Collection.BulkWriteAsync(models, cancellationToken: cancellationToken);
        _logger.LogInformation("Updated status for {Updated} entities", result.ModifiedCount);
    }
    
    public async Task UpdateEnvironmentState(PlatformStatePayload state, CancellationToken cancellationToken)
    {
        var env = state.Environment;
        var models = new List<WriteModel<Entity>>();

        foreach (var kv in state.Tenants)
        {
            var filter = Builders<Entity>.Filter.Eq(f => f.Name, kv.Key);
            var update = Builders<Entity>.Update.Set(e => e.Name, kv.Key)
                .Set(e => e.Envs[env], kv.Value.Tenant)
                .Set(e => e.Progress[env], kv.Value.Progress);
            
            // Update the metadata if its present.
            if (kv.Value.Metadata != null)
            {
                update.Set(e => e.Metadata, kv.Value.Metadata);
                // Copy the entity types to the root object
                if (!string.IsNullOrEmpty(kv.Value.Metadata?.Type) && Enum.TryParse<Type>(kv.Value.Metadata?.Type, true, out var entityType))
                {
                    update = update.Set(e => e.Type, entityType);
                }
                if (!string.IsNullOrEmpty(kv.Value.Metadata?.Subtype) && Enum.TryParse<SubType>(kv.Value.Metadata?.Subtype, true, out var entitySubType))
                {
                    update = update.Set(e => e.SubType, entitySubType);
                }
            }

            var m = new UpdateOneModel<Entity>(
                filter,
                update
            )
            { IsUpsert = true };

            models.Add(m);
        }

        // Remove environment for any service not in the list
        var removeMissing = new UpdateOneModel<Entity>(
            Builders<Entity>.Filter.Nin(f => f.Name, state.Tenants.Keys),
            Builders<Entity>.Update
                .Unset(e => e.Envs[env])
                .Unset(e => e.Progress[env])
        )
        { IsUpsert = false };
        models.Add(removeMissing);

        if (models.Count > 0)
        {
            var result = await Collection.BulkWriteAsync(models, cancellationToken: cancellationToken);
            _logger.LogInformation("Updated {Updated}, inserted {Inserted}, removed {Removed} tenants in {Env}",
                result.ModifiedCount, result.InsertedCount, result.DeletedCount, env);
        }
    }
}