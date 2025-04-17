using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Migrations;

public interface IDatabaseMigrationService
{
    public Task CreateMigration(DatabaseMigration migration, CancellationToken ct);
    public Task<DatabaseMigration?> Link(string cdpMigrationId, string buildId, CancellationToken ct);
    public Task<bool> UpdateStatus(string buildId, string status, DateTime timestamp, CancellationToken ct);

    public Task<DatabaseMigration?> FindByCdpMigrationId(string cdpMigrationId, CancellationToken ct);
    public Task<DatabaseMigration?> FindByBuildId(string buildId, CancellationToken ct);
    
    public Task<List<DatabaseMigration>> Find(DatabaseMigrationFilter filter, CancellationToken ct);
    public Task<DatabaseMigration?> FindOne(DatabaseMigrationFilter filter, CancellationToken ct);
    public Task<List<DatabaseMigration>> LatestForService(string service, CancellationToken ct);
}

public class DatabaseMigrationService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : 
    MongoService<DatabaseMigration>(connectionFactory, CollectionName, loggerFactory), IDatabaseMigrationService
{
    private const string CollectionName = "migrations";
    
    protected override List<CreateIndexModel<DatabaseMigration>> DefineIndexes(IndexKeysDefinitionBuilder<DatabaseMigration> builder)
    {
        var cdpMigrationId = new CreateIndexModel<DatabaseMigration>(builder.Ascending(m => m.CdpMigrationId));
        var buildId = new CreateIndexModel<DatabaseMigration>(builder.Ascending(m => m.BuildId));
        var serviceAndEnv = new CreateIndexModel<DatabaseMigration>(builder.Combine(
            builder.Ascending(m => m.Service),
            builder.Ascending(m => m.Environment)
        ));
        
        return
        [
            cdpMigrationId,
            buildId,
            serviceAndEnv
        ];
    }

    public async Task CreateMigration(DatabaseMigration migration, CancellationToken ct)
    {
        await Collection.InsertOneAsync(migration, cancellationToken: ct);
    }

    public async Task<DatabaseMigration?> Link(string cdpMigrationId, string buildId, CancellationToken ct)
    {
        var fb = Builders<DatabaseMigration>.Filter;
        var filter = fb.And(fb.Eq(m => m.CdpMigrationId, cdpMigrationId), fb.Eq(m => m.BuildId, null));
        var update = Builders<DatabaseMigration>.Update.Set(m => m.BuildId, buildId);
        await Collection.UpdateOneAsync(filter, update, null, ct);
        return Collection.Find(fb.Eq(m => m.CdpMigrationId, cdpMigrationId)).FirstOrDefault(ct);
    }

    public async Task<bool> UpdateStatus(string buildId, string status, DateTime timestamp, CancellationToken ct)
    {
        var fb = Builders<DatabaseMigration>.Filter;
        var filter = Builders<DatabaseMigration>.Filter.Eq(m => m.BuildId, buildId);
        var update = Builders<DatabaseMigration>.Update.Set(m => m.Status, status).Set(m => m.Updated, timestamp);
        var result = await Collection.UpdateOneAsync(filter, update, null, ct);

        return result.ModifiedCount > 0;
    }

    public async Task<DatabaseMigration?> FindByCdpMigrationId(string cdpMigrationId, CancellationToken ct)
    {
        return await Collection.Find(m => m.CdpMigrationId == cdpMigrationId).FirstOrDefaultAsync(ct);
    }

    public async Task<DatabaseMigration?> FindByBuildId(string buildId, CancellationToken ct)
    {
        return await Collection.Find(m => m.BuildId == buildId).FirstOrDefaultAsync(ct);
    }

    public async Task<List<DatabaseMigration>> Find(DatabaseMigrationFilter filter, CancellationToken ct)
    {
        return await Collection.Find(filter.Filter()).SortBy(m => m.Updated).ToListAsync(ct);
    }

    public async Task<DatabaseMigration?> FindOne(DatabaseMigrationFilter filter, CancellationToken ct)
    {
        return await Collection.Find(filter.Filter()).SortBy(m => m.Updated).FirstOrDefaultAsync(ct);
    }
    
    public async Task<List<DatabaseMigration>> LatestForService(string service, CancellationToken ct)
    {
        var pipeline = new EmptyPipelineDefinition<DatabaseMigration>()
            .Match(m => m.Service == service && m.Status == CodeBuildStatuses.Succeeded)
            .Sort(new SortDefinitionBuilder<DatabaseMigration>().Descending(d => d.Updated))
            .Group(d => new { d.Service, d.Environment }, grp => new { Root = grp.First() })
            .Project(grp => grp.Root);
            
        return await Collection.Aggregate(pipeline, cancellationToken: ct).ToListAsync(ct);
    }
}

public record DatabaseMigrationFilter(
    string? Service = null,
    string? Environment = null,
    string? BuildId = null,
    string? CdpMigrationId = null,
    string? Status = null
)
{
    public FilterDefinition<DatabaseMigration> Filter()
    {
        var builder = Builders<DatabaseMigration>.Filter;
        var filter = builder.Empty;

        if (Environment != null)
        {
            filter &= builder.Eq(t => t.Environment, Environment);
        }

        if (Service != null)
        {
            filter &= builder.Eq(t => t.Service, Service);
        }

        if (BuildId != null)
        {
            filter &= builder.Eq(t => t.BuildId, BuildId);
        } 

        if (CdpMigrationId != null)
        {
            filter &= builder.Ne(t => t.CdpMigrationId, CdpMigrationId);
        }

        if (Status != null)
        {
            filter &= builder.Ne(t => t.Status, Status);
        }

        return filter;
    }
}
