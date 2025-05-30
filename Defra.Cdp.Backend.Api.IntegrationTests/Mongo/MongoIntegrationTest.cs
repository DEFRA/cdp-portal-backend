using Testcontainers.MongoDb;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Mongo;

public class MongoIntegrationTest : IAsyncDisposable
{
    public string connectionString { get; set; }
    private MongoDbContainer _mongoDbContainer;

    public async Task InitializeAsync()
    {
        if (_mongoDbContainer != null)
        {
            await _mongoDbContainer.DisposeAsync();
        }

        // Initialize MongoDB container with a specific version
        _mongoDbContainer = new MongoDbBuilder()
            .WithImage("mongo:6.0")
            .Build();

        // Start the container
        await _mongoDbContainer.StartAsync();

        connectionString = _mongoDbContainer.GetConnectionString();
    }


    public async ValueTask DisposeAsync()
    {
        await _mongoDbContainer.StopAsync();
    }
}