using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Migrations;

public interface IDatabaseMigrationService
{
    public Task CreateMigration(DatabaseMigration migration, CancellationToken ct);
    public Task<DatabaseMigration?> Link(string cdpMigrationId, string buildId, CancellationToken ct);
    public Task<bool> UpdateStatus(string buildId, string status, DateTime timestamp, CancellationToken ct);

    public Task<DatabaseMigration?> FindByBuildId(string buildId, CancellationToken ct);
    public Task<List<DatabaseMigration>> FindByService(string service, CancellationToken ct);
}

public class DatabaseMigrationService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : 
    MongoService<DatabaseMigration>(connectionFactory, CollectionName, loggerFactory), IDatabaseMigrationService
{
    private const string CollectionName = "migrations";
    
    protected override List<CreateIndexModel<DatabaseMigration>> DefineIndexes(IndexKeysDefinitionBuilder<DatabaseMigration> builder)
    {
        return [];
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

    public async Task<DatabaseMigration?> FindByBuildId(string buildId, CancellationToken ct)
    {
        return await Collection.Find(m => m.BuildId == buildId).FirstOrDefaultAsync(ct);
    }

    public async Task<List<DatabaseMigration>> FindByService(string service, CancellationToken ct)
    {
        return await Collection.Find(m => m.Service == service).ToListAsync(ct);
    }
}