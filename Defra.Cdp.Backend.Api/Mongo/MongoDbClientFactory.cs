using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Mongo;

public class MongoDbClientFactory : IMongoDbClientFactory
{
    private readonly string _connectionString;
    private readonly IMongoDatabase _mongoDatabase;
    private IMongoClient _client;

    public MongoDbClientFactory(string? connectionString, string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("MongoDB connection string and database name cannot be empty");
        _connectionString = connectionString;
        _client = CreateClient();
        _mongoDatabase = _client.GetDatabase(databaseName);
    }

    public IMongoClient CreateClient()
    {
        var settings = MongoClientSettings.FromConnectionString(_connectionString);
        _client = new MongoClient(settings);
        var camelCaseConvention = new ConventionPack { new CamelCaseElementNameConvention() };
        // convention must be registered before initialising collection
        ConventionRegistry.Register("CamelCase", camelCaseConvention, type => true);
        return _client;
    }

    public IMongoCollection<T> GetCollection<T>(string collection)
    {
        return _mongoDatabase.GetCollection<T>(collection);
    }

    public IMongoCollection<T> InitCappedCollection<T>(string collection, long? maxDocuments = null, long? maxSize = null)
    {
        if (!_mongoDatabase.ListCollectionNames().ToEnumerable().Contains(collection))
        {
            _mongoDatabase.CreateCollection(collection,
                new CreateCollectionOptions
                {
                    Capped = true,
                    MaxDocuments = maxDocuments,
                    MaxSize = maxSize
                });
        }

        return _mongoDatabase.GetCollection<T>(collection);
    }
}