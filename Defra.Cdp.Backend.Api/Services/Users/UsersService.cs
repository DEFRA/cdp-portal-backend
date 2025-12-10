using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;


namespace Defra.Cdp.Backend.Api.Services.Users;

public interface IUsersService
{
    Task CreateUser(User user, CancellationToken cancellationToken = default);
    Task<bool> UpdateUser(User user, CancellationToken cancellationToken = default);
    Task<bool> DeleteUser(string userId, CancellationToken cancellationToken = default);
    Task<List<User>> FindAll(CancellationToken cancellationToken = default);
    Task<User?> Find(string userId, CancellationToken cancellationToken = default);
    
    Task SyncUsers(List<User> users, CancellationToken cancellationToken = default);
}

public class UsersService(IMongoDbClientFactory connectionFactory, ILoggerFactory loggerFactory)
    : MongoService<User>(connectionFactory, CollectionName, loggerFactory), IUsersService
{
    public const string CollectionName = "users";

    protected override List<CreateIndexModel<User>> DefineIndexes(IndexKeysDefinitionBuilder<User> builder)
    {
        var userIdIndex =
            new CreateIndexModel<User>(builder.Ascending(u => u.UserId), new CreateIndexOptions { Unique = true });
        var emailIndex =
            new CreateIndexModel<User>(builder.Ascending(u => u.Email), new CreateIndexOptions { Unique = true });
        return [userIdIndex, emailIndex];
    }

    public async Task CreateUser(User user, CancellationToken cancellationToken = default)
    {
        var exists = await Collection.Find(u => u.UserId == user.UserId)
            .AnyAsync(cancellationToken);

        if (exists)
        {
            throw new InvalidOperationException($"User {user.UserId} already exists.");
        }

        await Collection.InsertOneAsync(user, cancellationToken: cancellationToken);
    }

    public async Task<bool> UpdateUser(User user, CancellationToken cancellationToken = default)
    {
        var update = Builders<User>.Update
            .Set(u => u.Name, user.Name)
            .Set(u => u.Email, user.Email)
            .Set(u => u.Github, user.Github);

        var result = await Collection.UpdateOneAsync(
            u => u.UserId == user.UserId,
            update,
            cancellationToken: cancellationToken);

        return result.MatchedCount > 0;
    }

    public async Task<bool> DeleteUser(string userId, CancellationToken cancellationToken = default)
    {
        var result = await Collection.DeleteOneAsync(u => u.UserId == userId, cancellationToken);
        return result.DeletedCount > 0;
    }

    public async Task SyncUsers(List<User> users, CancellationToken cancellationToken = default)
    {
        var existingUsers = await Collection.Find(FilterDefinition<User>.Empty)
            .ToListAsync(cancellationToken);

        var incomingIds = users.Select(u => u.UserId).ToHashSet();

        var upserts = users.Select(user =>
        {
            var filter = Builders<User>.Filter.Eq(u => u.UserId, user.UserId);
            return new ReplaceOneModel<User>(filter, user) { IsUpsert = true };
        }).ToList();

        if (upserts.Count > 0)
        {
            await Collection.BulkWriteAsync(upserts, new BulkWriteOptions { IsOrdered = false }, cancellationToken);
        }

        var removedIds = existingUsers.Select(u => u.UserId)
            .Where(id => !incomingIds.Contains(id))
            .ToList();

        if (removedIds.Count > 0)
        {
            var deleteFilter = Builders<User>.Filter.In(u => u.UserId, removedIds);
            await Collection.DeleteManyAsync(deleteFilter, cancellationToken);
        }
    }

    public async Task<List<User>> FindAll(CancellationToken cancellationToken = default)
    {
        return await Collection.Find(FilterDefinition<User>.Empty).ToListAsync(cancellationToken);
    }

    public async Task<User?> Find(string userId, CancellationToken cancellationToken = default)
    {
        return await Collection.Find(u => u.UserId == userId)
            .FirstOrDefaultAsync(cancellationToken);
    }

}