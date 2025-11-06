using System.Net;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Team = Defra.Cdp.Backend.Api.Services.Entities.Model.Team;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Endpoints;

public class EntitiesEndpointTestSupport : MongoTestSupport
{
    private readonly IEntitiesService _entitiesService;

    // Create Server
    private readonly TestServer _server;

    public EntitiesEndpointTestSupport(MongoContainerFixture fixture) : base(fixture)
    {
        _entitiesService = new EntitiesService(CreateMongoDbClientFactory(), new NullLoggerFactory());

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSingleton(_entitiesService);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => { endpoints.MapEntitiesEndpoint(); });
            });

        // insert test data
        var tasks = TestData().Select(e => _entitiesService.Create(e, CancellationToken.None));
        Task.WaitAll(tasks.ToArray(), CancellationToken.None);

        _server = new TestServer(builder);
    }

    [Fact]
    public async Task Should_return_404_when_entity_is_missing()
    {
        var client = _server.CreateClient();
        var result = await client.GetAsync("/entities/missing-entity", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }

    [Fact]
    public async Task Should_return_entity_when_it_exists()
    {
        var client = _server.CreateClient();
        var result = await client.GetAsync("/entities/foo-frontend", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var resultAsString = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var entity = JsonSerializer.Deserialize<Entity>(resultAsString);
        Assert.Equal("foo-frontend", entity?.Name);
    }

    [Fact]
    public async Task Should_return_filters_for_everything()
    {
        var client = _server.CreateClient();
        var result = await client.GetAsync("/entities/filters", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var body = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var filters = JsonSerializer.Deserialize<EntitiesService.EntityFilters>(body);

        var allEntityNames = TestData().Select(e => e.Name).ToList();
        allEntityNames.Sort();
        Assert.Equal(allEntityNames, filters?.Entities);
    }


    [Fact]
    public async Task Should_return_filters_by_status()
    {
        var client = _server.CreateClient();
        var result = await client.GetAsync("/entities/filters?status=Creating", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var body = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var filters = JsonSerializer.Deserialize<EntitiesService.EntityFilters>(body);

        var allEntityNames = TestData().Where(e => e.Status == Status.Creating).Select(e => e.Name).ToList();
        allEntityNames.Sort();
        Assert.Equal(allEntityNames, filters?.Entities);
    }

    [Fact]
    public async Task Should_return_filters_by_type()
    {
        var client = _server.CreateClient();
        var result = await client.GetAsync("/entities/filters?type=TestSuite", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var body = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var filters = JsonSerializer.Deserialize<EntitiesService.EntityFilters>(body);

        var expected = TestData().Where(e => e.Type == Type.TestSuite).Select(e => e.Name).ToList();
        expected.Sort();
        Assert.Equal(expected, filters?.Entities);
    }

    [Fact]
    public async Task Should_list_all_entities()
    {
        var client = _server.CreateClient();
        var result = await client.GetAsync("/entities", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var body = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var entities = JsonSerializer.Deserialize<List<Entity>>(body);
        Assert.NotNull(entities);
        Assert.NotEmpty(entities);

        var expected = TestData().Select(e => e.Name).ToList();
        expected.Sort();
        var entitiesReturned = entities.Select(e => e.Name).ToList();
        Assert.Equal(expected, entitiesReturned);
    }


    [Fact]
    public async Task Should_list_entities_by_team()
    {
        var client = _server.CreateClient();
        var result = await client.GetAsync("/entities?teamIds=platform", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var body = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var entities = JsonSerializer.Deserialize<List<Entity>>(body);
        Assert.NotNull(entities);

        var expected = TestData().Where(e => e.Teams.Exists(t => t.TeamId == "platform")).Select(e => e.Name).ToList();
        expected.Sort();
        var entitiesReturned = entities.Select(e => e.Name).ToList();
        Assert.Equal(expected, entitiesReturned);
    }


    [Fact]
    public async Task Should_list_entities_by_type()
    {
        var client = _server.CreateClient();
        var result = await client.GetAsync("/entities?type=TestSuite", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var body = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var entities = JsonSerializer.Deserialize<List<Entity>>(body);
        Assert.NotNull(entities);
        Assert.NotEmpty(entities);

        Assert.True(entities.TrueForAll(e => e.Type == Type.TestSuite));
    }


    [Fact]
    public async Task Should_list_entities_by_status()
    {
        var client = _server.CreateClient();
        var result = await client.GetAsync("/entities?status=Creating", TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var body = await result.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var entities = JsonSerializer.Deserialize<List<Entity>>(body);
        Assert.NotNull(entities);
        Assert.NotEmpty(entities);

        Assert.True(entities.TrueForAll(e => e.Status == Status.Creating));
    }

    private static Entity[] TestData()
    {
        Entity[] entities =
        [
            new()
            {
                Name = "foo-frontend",
                Teams = [new Team { Name = "Platform", TeamId = "platform" }],
                Status = Status.Created,
                Type = Type.Microservice,
                SubType = SubType.Frontend,
            },
            new()
            {
                Name = "foo-backend",
                Teams = [new Team { Name = "Platform", TeamId = "platform" }],
                Status = Status.Created,
                Type = Type.Microservice,
                SubType = SubType.Backend
            },
            new()
            {
                Name = "bar-backend",
                Teams = [new Team { Name = "Tenant", TeamId = "tenant" }],
                Status = Status.Created,
                Type = Type.Microservice,
                SubType = SubType.Backend
            },
            new()
            {
                Name = "baz-tests",
                Teams =
                [
                    new Team { Name = "Platform", TeamId = "platform" }, new Team { Name = "Tenant", TeamId = "tenant" }
                ],
                Status = Status.Created,
                Type = Type.TestSuite
            },
            new()
            {
                Name = "not-done-yet-backend",
                Teams =
                [
                    new Team { Name = "Tenant", TeamId = "tenant" }
                ],
                Status = Status.Creating,
                Type = Type.Microservice,
                SubType = SubType.Backend
            }
        ];
        return entities;
    }
}