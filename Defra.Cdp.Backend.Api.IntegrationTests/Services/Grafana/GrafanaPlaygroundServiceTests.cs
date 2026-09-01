using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Services.Grafana;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;
using Microsoft.Extensions.Logging.Abstractions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Grafana;

public class GrafanaPlaygroundServiceTests(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{
    [Fact]
    public async Task test_load_and_save()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionFactory = CreateMongoDbClientFactory();
        var playgroundService = new GrafanaPlaygroundService(connectionFactory, new NullLoggerFactory());

        var resources = new GrafanaPlaygroundResources
        {
            Service = "foo",
            RequestId = "1234",
            Alerts = [],
            Dashboards = []
        };
        

        await playgroundService.UpdatePlaygroundForService(resources, ct);

        var fromDatabase = await playgroundService.FindPlaygroundsForService(resources.Service, ct);
        Assert.NotNull(fromDatabase);
        Assert.Equal(resources.RequestId, fromDatabase.RequestId);
        Assert.Equal(resources.Service, fromDatabase.Service);
        Assert.Equivalent(resources.Dashboards, fromDatabase.Dashboards);
        Assert.Equivalent(resources.Alerts, fromDatabase.Alerts);
    }
}