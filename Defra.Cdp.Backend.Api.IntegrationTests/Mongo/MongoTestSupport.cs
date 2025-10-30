using Defra.Cdp.Backend.Api.Mongo;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Mongo;

public class MongoTestSupport(MongoContainerFixture fixture) 
{
    /// <summary>
    /// Creates a MongoDBClientFactory. By default, it will generate a random database name, which will keep the
    /// DB connection isolated from other tests using the same container. 
    /// </summary>
    /// <param name="dbName"></param>
    /// <returns></returns>
    protected MongoDbClientFactory CreateMongoDbClientFactory(string? dbName = null)
    {
        if (dbName == null)
        {
            dbName = $"t{Guid.NewGuid():N}";
        }
        return new MongoDbClientFactory(fixture.Container.GetConnectionString(), dbName);
    }
}