using System.Net;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Endpoints;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
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

public class EntitiesEndpointTest : ServiceTest
{
    private readonly IMongoDbClientFactory _mongoFactory;
    private ILoggerFactory _loggerFactory = new NullLoggerFactory();
    private readonly IEntitiesService _entitiesService;
    private readonly IEntityStatusService _entityStatusService;

    // Create Server
    private readonly TestServer _server;

    public EntitiesEndpointTest(MongoIntegrationTest fixture) : base(fixture)
    {
        _mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "EntitiesEndpointTest");
        _entitiesService = new EntitiesService(_mongoFactory, _loggerFactory);
        _entityStatusService = NSubstitute.Substitute.For<IEntityStatusService>();

        var builder = new WebHostBuilder()
            .ConfigureServices(services =>
            {
                services.AddRouting();
                services.AddSingleton(_entitiesService);
                services.AddSingleton(_entityStatusService);
            })
            .Configure(app =>
            {
                app.UseRouting();
                app.UseEndpoints(endpoints => { endpoints.MapEntitiesEndpoint(); });
            });

        // insert test data
        var tasks = testData().Select(e => _entitiesService.Create(e, CancellationToken.None));
        Task.WaitAll(tasks.ToArray(), CancellationToken.None);

        _server = new TestServer(builder);
    }

    [Fact]
    public async Task Should_return_404_when_entity_is_missing()
    {
        var client = _server.CreateClient();
        var result = await client.GetAsync("/entities/missing-entity");
        Assert.Equal(HttpStatusCode.NotFound, result.StatusCode);
    }

    [Fact]
    public async Task Should_return_entity_when_it_exists()
    {
        var client = _server.CreateClient();
        var result = await client.GetAsync("/entities/foo-frontend");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var entity = FromJson<Entity>(await result.Content.ReadAsStringAsync());
        Assert.Equal("foo-frontend", entity.Name);
    }


    [Fact]
    public async Task Should_return_filters_for_everything()
    {
        var client = _server.CreateClient();
        var result = await client.GetAsync("/entities/filters");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var body = await result.Content.ReadAsStringAsync();
        var entity = JsonSerializer.Deserialize<EntitiesService.EntityFilters>(body);

        var allEntityNames = testData().Select(e => e.Name).ToList();
        allEntityNames.Sort();
        Assert.Equal(allEntityNames, entity?.Entities);
    }


    [Fact]
    public async Task Should_return_filters_by_status()
    {
        var client = _server.CreateClient();
        var result = await client.GetAsync("/entities/filters?status=Creating");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var body = await result.Content.ReadAsStringAsync();
        var entity = JsonSerializer.Deserialize<EntitiesService.EntityFilters>(body);

        var allEntityNames = testData().Where(e => e.Status == Status.Creating).Select(e => e.Name).ToList();
        allEntityNames.Sort();
        Assert.Equal(allEntityNames, entity?.Entities);
    }

    [Fact]
    public async Task Should_return_filters_by_type()
    {
        var client = _server.CreateClient();
        var result = await client.GetAsync("/entities/filters?type=TestSuite");
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);

        var body = await result.Content.ReadAsStringAsync();
        var entity = JsonSerializer.Deserialize<EntitiesService.EntityFilters>(body);

        var allEntityNames = testData().Where(e => e.Type == Type.TestSuite).Select(e => e.Name).ToList();
        allEntityNames.Sort();
        Assert.Equal(allEntityNames, entity?.Entities);
    }

    private Entity[] testData()
    {
        Entity[] entities =
        [
            new()
            {
                Name = "foo-frontend",
                Teams = [new Team { Name = "Platform", TeamId = "platform" }],
                Status = Status.Created,
                Type = Type.Microservice,
                SubType = SubType.Frontend
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