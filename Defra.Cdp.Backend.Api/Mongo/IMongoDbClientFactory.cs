using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Mongo;

public interface IMongoDbClientFactory
{
    protected IMongoClient CreateClient();

    IMongoCollection<T> GetCollection<T>(string collection);
}