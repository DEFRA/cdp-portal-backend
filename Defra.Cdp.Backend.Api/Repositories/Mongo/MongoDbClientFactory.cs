using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Repositories.Mongo;

public class MongoDbClientFactory : IMongoDbClientFactory
{
    private readonly string _connectionString;
    private readonly IMongoDatabase _mongoDatabase;
    private IMongoClient _client;

    public MongoDbClientFactory(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("MongoDB connection string cannot be empty");
        _connectionString = connectionString;
        _client = CreateClientAndDatabase();
        _mongoDatabase = _client.GetDatabase("cdp-backend");
    }

    public IMongoClient CreateClientAndDatabase()
    {
        _client = new MongoClient(_connectionString);
        var camelCaseConvention = new ConventionPack { new CamelCaseElementNameConvention() };
        // convention must be registered before initialising collection
        ConventionRegistry.Register("CamelCase", camelCaseConvention, type => true);
        return _client;
    }

    public IMongoCollection<T> GetCollection<T>(string collection)
    {
        var client = CreateClientAndDatabase();
        return _mongoDatabase.GetCollection<T>(collection);
    }
}