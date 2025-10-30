using Defra.Cdp.Backend.Api.Mongo;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Mongo;

public class MongoTestSupport(MongoContainerFixture fixture) : IClassFixture<MongoContainerFixture>
{
    
    protected MongoDbClientFactory CreateConnectionFactory(string? dbName = null)
    {
        if (dbName == null)
        {
            dbName = $"t{Guid.NewGuid():N}";
        }
        return new MongoDbClientFactory(fixture.Container.GetConnectionString(), dbName);
    }
}