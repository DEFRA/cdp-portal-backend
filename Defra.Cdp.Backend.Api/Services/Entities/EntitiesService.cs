using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using MongoDB.Bson;
using MongoDB.Driver;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Services.Entities;

public interface IEntitiesService
{
    Task<List<Entity>> GetEntities(Type? type, string? partialName, string? teamId, bool includeDecommissioned,
        CancellationToken cancellationToken);

    Task<EntitiesService.EntityFilters> GetFilters(Type type, CancellationToken cancellationToken);
    Task Create(Entity entity, CancellationToken cancellationToken);
    Task UpdateStatus(Status overallStatus, string repositoryName, CancellationToken cancellationToken);
    Task Decommission(string repositoryName, string userId, string userDisplayName, CancellationToken cancellationToken);
    Task<Entity> GetEntity(string repositoryName, CancellationToken cancellationToken);
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

    public async Task<List<Entity>> GetEntities(Type? type, string? partialName, string? teamId,
        bool includeDecommissioned,
        CancellationToken cancellationToken)
    {
        var builder = Builders<Entity>.Filter;
        var filter = builder.Empty;

        if (type != null)
        {
            filter = builder.Eq(e => e.Type, type);
        }
        
        if (teamId != null)
        {
            var teamFilter = builder.ElemMatch(d => d.Teams, t => t.TeamId == teamId);
            filter &= teamFilter;
        }

        if (!string.IsNullOrWhiteSpace(partialName))
        {
            var partialServiceFilter =
                builder.Regex(e => e.Name, new BsonRegularExpression(partialName, "i"));
            filter &= partialServiceFilter;
        }
        
        if (!includeDecommissioned)
        {
            var decommissionedFilter = builder.Eq(e => e.Decommissioned, null);
            filter &= decommissionedFilter;
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

    public async Task Create(Entity entity, CancellationToken cancellationToken)
    {
        await Collection.InsertOneAsync(entity, cancellationToken: cancellationToken);
    }

    public async Task UpdateStatus(Status overallStatus, string repositoryName, CancellationToken cancellationToken)
    {
        var filter = Builders<Entity>.Filter.Eq(entity => entity.Name, repositoryName);
        var update = Builders<Entity>.Update.Set(e => e.Status, overallStatus);
        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task Decommission(string repositoryName, string userId, string userDisplayName, CancellationToken cancellationToken)
    {
        var filter = Builders<Entity>.Filter.Eq(entity => entity.Name, repositoryName);
        var update = Builders<Entity>.Update.Set(e => e.Decommissioned, new Decommission
        {
            DecommissionedBy = new Person
            {
                Id = userId,
                Name = userDisplayName
            },
            DecommissionedAt = DateTime.UtcNow
        });
        await Collection.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
    }

    public async Task<Entity> GetEntity(string repositoryName, CancellationToken cancellationToken)
    {
        return await Collection.Find(e => e.Name == repositoryName)
            .FirstOrDefaultAsync(cancellationToken: cancellationToken);
    }

    public class EntityFilters
    {
        public List<string> Entities { get; set; } = new();
        public List<Team> Teams { get; set; } = new();
    }

    public class Team
    {
        public string TeamId { get; set; }
        public string Name { get; set; }
    }
}