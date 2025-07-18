using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using Team = Defra.Cdp.Backend.Api.Services.Entities.Model.Team;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Services.Entities;

public interface IEntitiesService
{
    Task<List<Entity>> GetEntities(Type? type, string? partialName, string[] teamIds, bool includeDecommissioned,
        CancellationToken cancellationToken);
    Task<List<Entity>> GetEntities(Type? type, string? partialName, string[] teamIds, Status[] statuses,
        CancellationToken cancellationToken);

    Task<EntitiesService.EntityFilters> GetFilters(Type type, CancellationToken cancellationToken);
    Task<bool> Create(Entity entity, CancellationToken cancellationToken);
    Task UpdateStatus(Status overallStatus, string entityName, CancellationToken cancellationToken);

    Task SetDecommissionDetail(string entityName, string userId, string userDisplayName,
        CancellationToken cancellationToken);

    Task<Entity?> GetEntity(string entityName, CancellationToken cancellationToken);
    Task<List<Entity>> GetCreatingEntities(CancellationToken cancellationToken);

    Task AddTag(string entityName, string tag, CancellationToken cancellationToken);
    Task RemoveTag(string entityName, string tag, CancellationToken cancellationToken);
    Task RefreshTeams(List<Repository> repos, CancellationToken cancellationToken);
    Task<List<Entity>> EntitiesPendingDecommission(CancellationToken cancellationToken);
    Task DecommissioningWorkflowsTriggered(string entityName, CancellationToken cancellationToken);
    Task DecommissionFinished(string entityName, CancellationToken contextCancellationToken);
}

public class EntitiesService(
    IMongoDbClientFactory connectionFactory,
    ILoggerFactory loggerFactory)
    : MongoService<Entity>(connectionFactory, CollectionName, loggerFactory), IEntitiesService
{
    private const string CollectionName = "entities";

    protected override List<CreateIndexModel<Entity>> DefineIndexes(
        IndexKeysDefinitionBuilder<Entity> builder)
    {
        return [new CreateIndexModel<Entity>(builder.Ascending(s => s.Name), new CreateIndexOptions { Unique = true })];
    }

    public async Task<List<Entity>> GetEntities(Type? type, string? partialName, string[] teamIds, bool includeDecommissioned, CancellationToken cancellationToken)
    {
        Status[] statuses = includeDecommissioned
            ? [Status.Creating, Status.Created, Status.Decommissioning, Status.Decommissioned]
            : [Status.Creating, Status.Created];
        return await GetEntities(type,  partialName, teamIds, statuses, cancellationToken);
    }

    public async Task<List<Entity>> GetEntities(Type? type, string? partialName, string[] teamIds, Status[] statuses, CancellationToken cancellationToken)
    {
        var builder = Builders<Entity>.Filter;
        var filter = builder.Empty;

        if (type != null)
        {
            filter = builder.Eq(e => e.Type, type);
        }

        if (teamIds.Length > 0)
        {
            var teamFilter = builder.ElemMatch(e => e.Teams, t => t.TeamId != null && teamIds.Contains(t.TeamId));
            filter &= teamFilter;
        }

        if (!string.IsNullOrWhiteSpace(partialName))
        {
            var partialServiceFilter =
                builder.Regex(e => e.Name, new BsonRegularExpression(partialName, "i"));
            filter &= partialServiceFilter;
        }

        if (statuses.Length > 0)
        {
            var statusFilter = builder.In(e => e.Status, statuses);
            filter &= statusFilter;
        }
        
        return await Collection.Find(filter).ToListAsync(cancellationToken);
    }

    public async Task<EntityFilters> GetFilters(Type type, CancellationToken cancellationToken)
    {
        var pipeline = new[]
        {
            new BsonDocument("$match", new BsonDocument("type", type.ToString())), new BsonDocument("$facet",
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

    public async Task<bool> Create(Entity entity, CancellationToken cancellationToken)
    {
        try
        {
            await Collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            Logger.LogError("Duplicate key error: " + ex.Message);
            return false;
        }
        catch (Exception ex)
        {
            Logger.LogError("General insert error: " + ex.Message);
            throw;
        }
    }

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
        return await GetEntities(null, null, [], [Status.Creating], cancellationToken);
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
        return await GetEntities(null, null, [], [Status.Decommissioning], cancellationToken);
    }

    public async Task DecommissioningWorkflowsTriggered(string entityName, CancellationToken cancellationToken)
    {
        var filter = Builders<Entity>.Filter.Eq(entity => entity.Name, entityName);
        var update = Builders<Entity>.Update.Set(e => e.Decommissioned.WorkflowsTriggered, true);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task DecommissionFinished(string entityName, CancellationToken cancellationToken)
    {
        var filter = Builders<Entity>.Filter.Eq(entity => entity.Name, entityName);
        var update = Builders<Entity>.Update.Combine(
        Builders<Entity>.Update.Set(e => e.Decommissioned.Finished, DateTime.UtcNow),
            Builders<Entity>.Update.Set(e => e.Status, Status.Decommissioned));
        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public class EntityFilters
    {
        public List<string> Entities { get; set; } = [];
        public List<Team> Teams { get; set; } = [];
    }
}