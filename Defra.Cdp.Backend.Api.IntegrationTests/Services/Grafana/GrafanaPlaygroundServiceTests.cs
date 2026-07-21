using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Services.Grafana;
using Defra.Cdp.Backend.Api.Services.MonoLambda;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Triggers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Grafana;

public class GrafanaPlaygroundServiceTests(MongoContainerFixture fixture) : MongoTestSupport(fixture)
{
    private IMonoLambdaTrigger _monoLambdaTrigger = Substitute.For<IMonoLambdaTrigger>();
    
    [Fact]
    public async Task test_load_and_save()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionFactory = CreateMongoDbClientFactory();
        var playgroundService = new GrafanaPlaygroundService(connectionFactory, _monoLambdaTrigger, new NullLoggerFactory());

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
    
    [Fact]
    public async Task Test_WaitForUpdate_returns_if_recent_record_available()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionFactory = CreateMongoDbClientFactory();
        var playgroundService = new GrafanaPlaygroundService(connectionFactory, _monoLambdaTrigger, new NullLoggerFactory());

        var resources = new GrafanaPlaygroundResources
        {
            Service = "bar",
            RequestId = "1234",
            Alerts = [],
            Dashboards = []
        };

        await playgroundService.UpdatePlaygroundForService(resources, ct);

        var fromDatabase = await playgroundService.WaitForUpdate(resources.RequestId, 300, ct);
        Assert.NotNull(fromDatabase);
        Assert.Equal(resources.RequestId, fromDatabase.RequestId);
        Assert.Equal(resources.Service, fromDatabase.Service);
        Assert.Equivalent(resources.Dashboards, fromDatabase.Dashboards);
        Assert.Equivalent(resources.Alerts, fromDatabase.Alerts);
        Assert.True( (DateTime.UtcNow - fromDatabase.Updated).TotalSeconds < 30);
    }
    
    [Fact]
    public async Task Test_WaitForUpdate_returns_if_record_created_inside_timeframe()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionFactory = CreateMongoDbClientFactory();
        var playgroundService = new GrafanaPlaygroundService(connectionFactory, _monoLambdaTrigger, new NullLoggerFactory());

        var resources = new GrafanaPlaygroundResources
        {
            Service = "baz",
            RequestId = "4444",
            Alerts = [],
            Dashboards = [],
            Updated = DateTime.UtcNow.Subtract(TimeSpan.FromDays(3))
        };

        var fromDatabaseTask = playgroundService.WaitForUpdate(resources.RequestId, 2000, ct);
        await playgroundService.UpdatePlaygroundForService(resources, ct);

        var fromDatabase = await fromDatabaseTask;
        
        Assert.NotNull(fromDatabase);
    }
    
    [Fact]
    public async Task Test_RequestUpdateForService_triggers_lambda()
    {
        var ct = TestContext.Current.CancellationToken;
        var connectionFactory = CreateMongoDbClientFactory();
        var playgroundService = new GrafanaPlaygroundService(connectionFactory, _monoLambdaTrigger, new NullLoggerFactory());
        await playgroundService.RequestUpdateForService("foo", ct);

        await _monoLambdaTrigger.Received().Trigger(
            Arg.Is<MonoLambdaTriggerEvent<GrafanaListPlaygroundsTrigger>>(e => e.EventType=="grafana_list_playgrounds" && e.Payload.Service == "foo"), 
            Arg.Is("dev"), 
            Arg.Any<CancellationToken>());
    }
}