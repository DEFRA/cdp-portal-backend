using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Mongo;

public class Lock
{
    public string Id { get; set; }
    public DateTime ExpiresAt { get; set; }

    public Lock(string id, DateTime expiresAt)
    {
        Id = id;
        ExpiresAt = expiresAt;
    }
}

public class MongoLock : MongoService<Lock>
{
    private readonly ILogger<MongoLock> _logger;

    protected override List<CreateIndexModel<Lock>> DefineIndexes(IndexKeysDefinitionBuilder<Lock> builder)
    {
        // TTL is set to zero on the assumption the field its checking is set to a value in the future. 
        // This lets us have a TTL per lock rather than a fixed on at a collection level.
        var expiresAfter = new CreateIndexOptions { ExpireAfter = TimeSpan.FromSeconds(0) };
        var ttlIndex = new CreateIndexModel<Lock>(builder.Ascending(l => l.ExpiresAt), expiresAfter);
        return [ttlIndex];
    }

    public MongoLock(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory) : base(connectionFactory, "locks", loggerFactory)
    {
        _logger = loggerFactory.CreateLogger<MongoLock>();
    }

    public async Task<bool> Lock(string lockId, TimeSpan duration, CancellationToken ct = new())
    {
        try
        {
            await Collection.InsertOneAsync(new Lock(lockId, DateTime.Now.Add(duration)), cancellationToken: ct);
            _logger.LogInformation("Claimed lock {lockId}", lockId);
            return true;
        }
        catch (Exception e)
        {
            _logger.LogWarning("Failed to lock {lockId}", lockId);
            _logger.LogTrace("Failed to lock {e}", e.Message);
            return false;
        }
    }

    public async Task Unlock(string lockId, CancellationToken ct = new())
    {
        try
        {
            var filter = new FilterDefinitionBuilder<Lock>().Eq(l => l.Id, lockId);
            await Collection.DeleteOneAsync(filter, ct);
            _logger.LogInformation("Released lock {lockId}", lockId);
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to release lock {lockId}, {e}", lockId, e);
            throw;
        }
    }
}