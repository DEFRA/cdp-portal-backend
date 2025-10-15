using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Tenants.Models;
using MongoDB.Driver;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Services.Tenants;

public interface ITenantService
{
    Task UpdateState(PlatformStatePayload state, CancellationToken cancellationToken);
    Task<Tenant?> FindOneAsync(string name, CancellationToken cancellation);
    Task<Tenant?> FindOneAsync(TenantFilter f, CancellationToken cancellation);
    Task<List<Tenant>> FindAsync(TenantFilter f, CancellationToken cancellation);
}

public class TenantService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<Tenant>(connectionFactory, CollectionName, loggerFactory), ITenantService
{
    private const string CollectionName = "tenants";

    private readonly ILogger<TenantService> _logger = loggerFactory.CreateLogger<TenantService>();
    
    protected override List<CreateIndexModel<Tenant>> DefineIndexes(IndexKeysDefinitionBuilder<Tenant> builder)
    {
        return [
            new CreateIndexModel<Tenant>(builder.Ascending(t => t.Name)),
            new CreateIndexModel<Tenant>(builder.Ascending(t => t.Metadata!.Teams))
        ];
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

    public async Task<Tenant?> FindOneAsync(string name, CancellationToken cancellation)
    {
        return await Collection.Find(t => t.Name == name).FirstOrDefaultAsync(cancellation);
    }

    public async Task<Tenant?> FindOneAsync(TenantFilter f, CancellationToken cancellation)
    {
        return await Collection.Find(f.Filter()).FirstOrDefaultAsync(cancellation);
    }

    
    public async Task<List<Tenant>> FindAsync(TenantFilter f, CancellationToken cancellation)
    {
        return await Collection.Find(f.Filter()).ToListAsync(cancellation);
    }
}

public record TenantFilter(
    string? TeamId = null,
    string? Environment = null,
    string? Name = null,
    bool HasPostgres = false,
    Type? EntityType = null,
    SubType? EntitySubType = null)
{
    public FilterDefinition<Tenant> Filter()
    {
        var builder = Builders<Tenant>.Filter;
        var filter = builder.Empty;

        if (TeamId != null)
        {
            filter &= builder.AnyEq(t => t.Metadata!.Teams, TeamId);
        }

        if (Environment != null)
        {
            // TODO: check we want $exists and not $ne: null
            filter &= builder.Exists(t => t.Envs[Environment]);
        }

        if (Name != null)
        {
            filter &= builder.Eq(t => t.Name, Name);
        }

        if (EntityType != null)
        {
            filter &= builder.Eq(t => t.Metadata!.Type, EntityType.ToString());
        }
        
        if (EntitySubType != null)
        {
            filter &= builder.Eq(t => t.Metadata!.Subtype, EntitySubType.ToString());
        }

        if (HasPostgres && Environment != null)
        {
            filter &= builder.Ne(t => t.Envs[Environment].SqlDatabase, null);
        }

        return filter;
    }
}
