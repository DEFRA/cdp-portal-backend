using Defra.Cdp.Backend.Api.Config;
using Microsoft.Extensions.Options;
using MongoDB.Bson.Serialization.Conventions;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Mongo;

public class MongoDbClientFactory : IMongoDbClientFactory
{
    private readonly IMongoDatabase _mongoDatabase;
    private IMongoClient _client;

    public MongoDbClientFactory(IOptions<MongoConfig> config)
    {
        var uri = config.Value.DatabaseUri;
        var databaseName = config.Value.DatabaseName;

        if (string.IsNullOrWhiteSpace(uri))
            throw new ArgumentException("MongoDB uri string cannot be empty");

        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("MongoDB database name cannot be empty");

        _client = CreateClient(uri);
        _mongoDatabase = _client.GetDatabase(databaseName);
    }
    
    public MongoDbClientFactory(string? connectionString, string? databaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString) || string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("MongoDB connection string and database name cannot be empty");
        _client = CreateClient(connectionString);
        _mongoDatabase = _client.GetDatabase(databaseName);
    }

    private IMongoClient CreateClient(string connectionString)
    {
        var settings = MongoClientSettings.FromConnectionString(connectionString);
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