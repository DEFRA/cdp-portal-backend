using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.GithubWorkflowEvents;

public class TenantServiceServiceTest(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task WillUpdateTeamsInTenantData()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TenantServices");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var tenantServicesService = new TenantServicesService(mongoFactory, repositoryService, new NullLoggerFactory());

        await repositoryService.Upsert(_fooRepository, CancellationToken.None);

        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
            {
                EventType = "tenant-services", Timestamp = DateTime.Now, Payload = _sampleEvent
            }
            , CancellationToken.None);

        // Create existing data
        var resultFoo =
            await tenantServicesService.FindOne(new TenantServiceFilter { name = "foo", environment = "test" },
                CancellationToken.None);
        Assert.Equivalent(resultFoo?.Teams, _fooRepository.Teams);

        // Update teams in repositories service
        var updatedFooRepository = new Repository
        {
            Id = "foo",
            Teams = [new RepositoryTeam("bar-team", "9999", "bar-team")],
            IsArchived = false,
            IsTemplate = false,
            IsPrivate = false
        };
        await repositoryService.Upsert(updatedFooRepository, CancellationToken.None);

        // Trigger another update
        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
            {
                EventType = "tenant-services", Timestamp = DateTime.Now, Payload = _sampleEvent
            }
            , CancellationToken.None);

        var updatedResults =
            await tenantServicesService.FindOne(new TenantServiceFilter { name = "foo", environment = "test" },
                CancellationToken.None);
        Assert.Equivalent(updatedResults?.Teams, updatedFooRepository.Teams);
    }


    [Fact]
    public async Task WillAddTeamToTenantData()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TenantServices");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var tenantServicesService =
            new TenantServicesService(mongoFactory, repositoryService, new NullLoggerFactory());

        await repositoryService.Upsert(_fooRepository, CancellationToken.None);

        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
            {
                EventType = "tenant-services", Timestamp = DateTime.Now, Payload = _sampleEvent
            }
            , CancellationToken.None);

        var resultFoo =
            await tenantServicesService.FindOne(new TenantServiceFilter { name = "foo", environment = "test" },
                CancellationToken.None);
        Assert.Equivalent(resultFoo?.Teams, new List<RepositoryTeam> { new("foo-team", "1234", "foo-team") });

        var resultBar =
            await tenantServicesService.FindOne(new TenantServiceFilter { name = "bar", environment = "test" },
                CancellationToken.None);

        Assert.NotNull(resultBar?.Teams);
        Assert.Empty(resultBar.Teams);
    }


    [Fact]
    public async Task WillRemoveDeletedTenants()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TenantServices");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var tenantServicesService =
            new TenantServicesService(mongoFactory, repositoryService, new NullLoggerFactory());

        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
            {
                EventType = "tenant-services", Timestamp = DateTime.Now, Payload = _sampleEvent
            }
            , CancellationToken.None);

        var resultFoo =
            await tenantServicesService.FindOne(new TenantServiceFilter { name = "foo", environment = "test" },
                CancellationToken.None);
        var resultBar =
            await tenantServicesService.FindOne(new TenantServiceFilter { name = "bar", environment = "test" },
                CancellationToken.None);
        Assert.NotNull(resultFoo);
        Assert.NotNull(resultBar);

        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
            {
                EventType = "tenant-services",
                Timestamp = DateTime.Now,
                Payload = new() { Environment = "test", Services = [_sampleEvent.Services[0]] }
            }
            , CancellationToken.None);
        resultFoo = await tenantServicesService.FindOne(new TenantServiceFilter { name = "foo", environment = "test" },
            CancellationToken.None);
        resultBar = await tenantServicesService.FindOne(new TenantServiceFilter { name = "bar", environment = "test" },
            CancellationToken.None);
        Assert.NotNull(resultFoo);
        Assert.Null(resultBar);
    }


    [Fact]
    public async Task WillFilterResults()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TenantServices");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var tenantServicesService =
            new TenantServicesService(mongoFactory, repositoryService, new NullLoggerFactory());

        await repositoryService.Upsert(_fooRepository, CancellationToken.None);
        await repositoryService.Upsert(_fooTestRepository, CancellationToken.None);

        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
            {
                EventType = "tenant-services", Timestamp = DateTime.Now, Payload = _sampleEvent
            }
            , CancellationToken.None);


        // Find By Name
        var result = await tenantServicesService.Find(new TenantServiceFilter { name = "foo" }, CancellationToken.None);
        Assert.Single(result);
        Assert.Equal("foo", result[0].ServiceName);

        // Find By Env
        result = await tenantServicesService.Find(new TenantServiceFilter { environment = "test" },
            CancellationToken.None);
        Assert.Equal(3, result.Count);

        result = await tenantServicesService.Find(new TenantServiceFilter { environment = "prod" },
            CancellationToken.None);
        Assert.Empty(result);

        // Find by Team
        result = await tenantServicesService.Find(new TenantServiceFilter { team = "foo-team" },
            CancellationToken.None);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.ServiceName == "foo");
        Assert.Contains(result, t => t.ServiceName == "foo-tests");

        // Find by Team and name
        result = await tenantServicesService.Find(new TenantServiceFilter(team: "foo-team", name: "foo"),
            CancellationToken.None);
        Assert.Single(result);
        Assert.Contains(result, t => t.ServiceName == "foo");

        // Find test suites
        result = await tenantServicesService.Find(new TenantServiceFilter { isTest = true }, CancellationToken.None);
        Assert.Single(result);
        Assert.Contains(result, t => t.ServiceName == "foo-tests");

        // Find services suites
        result = await tenantServicesService.Find(new TenantServiceFilter { isService = true }, CancellationToken.None);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.ServiceName == "foo");
        Assert.Contains(result, t => t.ServiceName == "bar");
    }


    private readonly TenantServicesPayload _sampleEvent = new()
    {
        Environment = "test",
        Services =
        [
            new Service
            {
                Name = "foo",
                Zone = "public",
                Mongo = false,
                Redis = true,
                ServiceCode = "FOO"
            },
            new Service
            {
                Name = "bar",
                Zone = "public",
                Mongo = false,
                Redis = true,
                ServiceCode = "BAR"
            },
            new Service
            {
                Name = "foo-tests",
                Zone = "public",
                Mongo = false,
                Redis = false,
                TestSuite = "foo-tests",
                ServiceCode = "FOO"
            }
        ]
    };

    private readonly Repository _fooRepository = new()
    {
        Id = "foo",
        Teams = [new RepositoryTeam("foo-team", "1234", "foo-team")],
        IsArchived = false,
        IsTemplate = false,
        IsPrivate = false
    };


    private readonly Repository _fooTestRepository = new()
    {
        Id = "foo-tests",
        Teams = [new RepositoryTeam("foo-team", "1234", "foo-team")],
        IsArchived = false,
        IsTemplate = false,
        IsPrivate = false
    };
}