using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Repositories.Mongo;

public interface IMongoDbClientFactory
{
    protected IMongoClient CreateClientAndDatabase();

    IMongoCollection<T> GetCollection<T>(string collection);
}