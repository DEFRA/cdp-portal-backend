using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using MongoDB.Driver;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Services.Tenants;

public class TenantService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<Tenant>(connectionFactory, CollectionName, loggerFactory)
{
    private const string CollectionName = "tenants";

    private readonly ILogger<TenantService> _logger = loggerFactory.CreateLogger<TenantService>();
    
    protected override List<CreateIndexModel<Tenant>> DefineIndexes(IndexKeysDefinitionBuilder<Tenant> builder)
    {
        // TODO: add indexes
        return [];
    }
    
    public async Task UpdateState(PlatformStatePayload state, CancellationToken cancellationToken)
    {
        var env = state.Environment;
        var models = new List<WriteModel<Tenant>>();
        foreach (var kv in state.Tenants)
        {
            var m = new UpdateOneModel<Tenant>(
                Builders<Tenant>.Filter.Eq(f => f.Name, kv.Key),
                Builders<Tenant>.Update
                    .Set(t => t.Name, kv.Key)
                    .Set(t => t.Envs[env], kv.Value.Tenant)
                    .Set(t => t.Metadata, kv.Value.Metadata)
            ) { IsUpsert = true };

            models.Add(m);
        }
        
        // Remove environment for any service not in the list
        var removeMissing = new UpdateOneModel<Tenant>(
            Builders<Tenant>.Filter.Nin(f => f.Name, state.Tenants.Keys),
            Builders<Tenant>.Update
                .Unset(t => t.Envs[env])
        ) { IsUpsert = false };
        models.Add(removeMissing);
        
        if (models.Count > 0)
        {
            var result = await Collection.BulkWriteAsync(models, cancellationToken: cancellationToken);
            _logger.LogInformation("Updated {Updated}, inserted {Inserted}, removed {Removed} tenants in {Env}", 
                result.ModifiedCount, result.InsertedCount, result.DeletedCount, env);
        }
    }

    public async Task<CdpTenant?> FindEnv(string env, string name)
    {
        var filter = Builders<Tenant>.Filter.And(Filters.ExistsInEnv(env), Filters.ByName(name));
        return await Collection.Find(filter).Project(t => t.Envs[env]).FirstOrDefaultAsync();
    }
}

public class Filters
{
#pragma warning disable CS8602 // Dereference of a possibly null reference.
    public static FilterDefinition<Tenant> ByName(string name)
    {
        return Builders<Tenant>.Filter.Eq(t => t.Name, name);
    }
    
    public static FilterDefinition<Tenant> ExistsInEnv(string env)
    {
        return Builders<Tenant>.Filter.Exists(t => t.Envs[env]);
    }
    
    public static FilterDefinition<Tenant> ByTeam(string team)
    {
        return Builders<Tenant>.Filter.ElemMatch(t => t.Metadata.Teams, team);
    }
    
    public static FilterDefinition<Tenant> ByType(Type entityType)
    {
        var typeFilter = Builders<Tenant>.Filter.Eq(t => t.Metadata.Type, entityType.ToString());
        return typeFilter;
    }
    
    public static FilterDefinition<Tenant> ByType(Type entityType, SubType entitySubType)
    {
        var fb = Builders<Tenant>.Filter;
        var typeFilter = fb.Eq(t => t.Metadata.Type, entityType.ToString());
        var subtypeFilter = fb.Eq(t => t.Metadata.Subtype, entityType.ToString());
        return fb.And(typeFilter, subtypeFilter);
    }
    
#pragma warning restore CS8602 // Dereference of a possibly null reference.
}