using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Microsoft.Extensions.Logging.Abstractions;

namespace Defra.Cdp.Backend.Api.IntegrationTests.GithubWorkflowEvents;

public class TenantServiceServiceTest(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    [Fact]
    public async Task WillUpdateTeamsInTenantData()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TenantServices");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var envLookup = new MockEnvironmentLookup();
        var tenantServicesService = new TenantServicesService(mongoFactory, repositoryService, envLookup, new NullLoggerFactory());

        await repositoryService.Upsert(_fooRepository, CancellationToken.None);

        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
        {
            EventType = "tenant-services",
            Timestamp = DateTime.Now,
            Payload = _sampleEvent
        }
            , CancellationToken.None);

        // Create existing data
        var resultFoo =
            await tenantServicesService.FindOne(new TenantServiceFilter { Name = "foo", Environment = "test" },
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
            EventType = "tenant-services",
            Timestamp = DateTime.Now,
            Payload = _sampleEvent
        }
            , CancellationToken.None);

        var updatedResults =
            await tenantServicesService.FindOne(new TenantServiceFilter { Name = "foo", Environment = "test" },
                CancellationToken.None);
        Assert.Equivalent(updatedResults?.Teams, updatedFooRepository.Teams);
    }


    [Fact]
    public async Task WillAddTeamToTenantData()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TenantServices");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var envLookup = new MockEnvironmentLookup();
        var tenantServicesService =
            new TenantServicesService(mongoFactory, repositoryService, envLookup, new NullLoggerFactory());

        await repositoryService.Upsert(_fooRepository, CancellationToken.None);

        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
        {
            EventType = "tenant-services",
            Timestamp = DateTime.Now,
            Payload = _sampleEvent
        }
            , CancellationToken.None);

        var resultFoo =
            await tenantServicesService.FindOne(new TenantServiceFilter { Name = "foo", Environment = "test" },
                CancellationToken.None);
        Assert.Equivalent(resultFoo?.Teams, new List<RepositoryTeam> { new("foo-team", "1234", "foo-team") });

        var resultBar =
            await tenantServicesService.FindOne(new TenantServiceFilter { Name = "bar", Environment = "test" },
                CancellationToken.None);

        Assert.NotNull(resultBar?.Teams);
        Assert.Empty(resultBar.Teams);
    }


    [Fact]
    public async Task WillRemoveDeletedTenants()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TenantServices");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var envLookup = new MockEnvironmentLookup();
        var tenantServicesService =
            new TenantServicesService(mongoFactory, repositoryService, envLookup, new NullLoggerFactory());

        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
        {
            EventType = "tenant-services",
            Timestamp = DateTime.Now,
            Payload = _sampleEvent
        }
            , CancellationToken.None);

        var resultFoo =
            await tenantServicesService.FindOne(new TenantServiceFilter { Name = "foo", Environment = "test" },
                CancellationToken.None);
        var resultBar =
            await tenantServicesService.FindOne(new TenantServiceFilter { Name = "bar", Environment = "test" },
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
        resultFoo = await tenantServicesService.FindOne(new TenantServiceFilter { Name = "foo", Environment = "test" },
            CancellationToken.None);
        resultBar = await tenantServicesService.FindOne(new TenantServiceFilter { Name = "bar", Environment = "test" },
            CancellationToken.None);
        Assert.NotNull(resultFoo);
        Assert.Null(resultBar);
    }


    [Fact]
    public async Task WillFilterResults()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TenantServices");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var envLookup = new MockEnvironmentLookup();
        var tenantServicesService =
            new TenantServicesService(mongoFactory, repositoryService, envLookup, new NullLoggerFactory());
        await repositoryService.Upsert(_fooRepository, CancellationToken.None);
        await repositoryService.Upsert(_fooTestRepository, CancellationToken.None);

        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
        {
            EventType = "tenant-services",
            Timestamp = DateTime.Now,
            Payload = _sampleEvent
        }
            , CancellationToken.None);


        // Find By Name
        var result = await tenantServicesService.Find(new TenantServiceFilter { Name = "foo" }, CancellationToken.None);
        Assert.Single(result);
        Assert.Equal("foo", result[0].ServiceName);

        // Find By Env
        result = await tenantServicesService.Find(new TenantServiceFilter { Environment = "test" },
            CancellationToken.None);
        Assert.Equal(4, result.Count);

        result = await tenantServicesService.Find(new TenantServiceFilter { Environment = "prod" },
            CancellationToken.None);
        Assert.Empty(result);

        // Find by Team
        result = await tenantServicesService.Find(new TenantServiceFilter { Team = "foo-team" },
            CancellationToken.None);
        Assert.Equal(2, result.Count);
        Assert.Contains(result, t => t.ServiceName == "foo");
        Assert.Contains(result, t => t.ServiceName == "foo-tests");

        // Find by Team and name
        result = await tenantServicesService.Find(new TenantServiceFilter(Team: "foo-team", Name: "foo"),
            CancellationToken.None);
        Assert.Single(result);
        Assert.Contains(result, t => t.ServiceName == "foo");

        // Find by TeamId and name
        result = await tenantServicesService.Find(new TenantServiceFilter(TeamId: "1234", Name: "foo"),
            CancellationToken.None);
        Assert.Single(result);
        Assert.Contains(result, t => t.ServiceName == "foo");

        // Find test suites
        result = await tenantServicesService.Find(new TenantServiceFilter { IsTest = true }, CancellationToken.None);
        Assert.Single(result);
        Assert.Contains(result, t => t.ServiceName == "foo-tests");

        // Find services suites
        result = await tenantServicesService.Find(new TenantServiceFilter { IsService = true }, CancellationToken.None);
        Assert.Equal(3, result.Count);
        Assert.Contains(result, t => t.ServiceName == "foo");
        Assert.Contains(result, t => t.ServiceName == "bar");

        // Find services with postgres
        result = await tenantServicesService.Find(new TenantServiceFilter { HasPostgres = true }, CancellationToken.None);
        Assert.Single(result);
        Assert.Contains(result, t => t.ServiceName == "postgres-service");
    }
  
    [Fact]
    public async Task WillRefreshTeams()
    {
        var logger = new NullLoggerFactory();
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TenantServices");
        var repositoryService = new RepositoryService(mongoFactory, logger);
        var envLookup = new MockEnvironmentLookup();
        var tenantServicesService =
            new TenantServicesService(mongoFactory, repositoryService, envLookup, new NullLoggerFactory());

        await repositoryService.Upsert(_fooRepository, CancellationToken.None);
        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
            {
                EventType = "tenant-services", Timestamp = DateTime.Now, Payload = _sampleEvent
            }
            , CancellationToken.None);

        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
            {
                EventType = "tenant-services", Timestamp = DateTime.Now, Payload = new TenantServicesPayload
                {
                    Environment = "prod",
                    Services = _sampleEvent.Services
                }
            }
            , CancellationToken.None);


        // Check initial state
        var tenant = await tenantServicesService.FindOne(new TenantServiceFilter { Name = "foo" }, CancellationToken.None);
        Assert.NotNull(tenant);
        Assert.Equal( "foo-team", tenant.Teams?[0].Github);

        // Update the teams in github
        List<Repository> repos =
        [
            new Repository
            {
                Id = "foo",
                Teams = [new RepositoryTeam("bar-team", "9999", "bar-team")],
                IsArchived = false,
                IsTemplate = false,
                IsPrivate = false
            }
        ];

        // Force refresh
        await tenantServicesService.RefreshTeams(repos, CancellationToken.None);

        var tenantProd = await tenantServicesService.FindOne(new TenantServiceFilter { Name = "foo", Environment = "prod"}, CancellationToken.None);
        Assert.NotNull(tenantProd);
        Assert.Equal( "bar-team", tenantProd.Teams?[0].Github);
        
        var tenantTest = await tenantServicesService.FindOne(new TenantServiceFilter { Name = "foo", Environment = "test"}, CancellationToken.None);
        Assert.NotNull(tenantTest);
        Assert.Equal( "bar-team", tenantTest.Teams?[0].Github);
    }
    
    [Fact]
    public async Task WillUseS3BucketUrl()
    {
        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "TenantServices");
        var repositoryService = new RepositoryService(mongoFactory, new NullLoggerFactory());
        var envLookup = new MockEnvironmentLookup();
        var tenantServicesService =
            new TenantServicesService(mongoFactory, repositoryService, envLookup, new NullLoggerFactory());

        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
        {
            EventType = "tenant-services",
            Timestamp = DateTime.Now,
            Payload = _sampleEvent
        }
            , CancellationToken.None);


        // Find By Name
        var result = await tenantServicesService.Find(new TenantServiceFilter { Name = "foo", Environment = "test"}, CancellationToken.None);
        Assert.Single(result);
        Assert.Equal(2, result[0].S3Buckets?.Count);
        Assert.Equal($"s3://foo-bucket-{envLookup.FindS3BucketSuffix("test")}" , result[0].S3Buckets![0].Url);
        Assert.Equal("s3://legacy-12345", result[0].S3Buckets![1].Url);
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
                ServiceCode = "FOO",
                S3Buckets = [
                    new Service.S3Bucket{ Name = "foo-bucket" },
                    new Service.S3Bucket{ Name = "legacy-bucket", Url = "s3://legacy-12345" }
                ]
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
            },
            new Service
            {
                Name = "postgres-service",
                Zone = "private",
                Mongo = false,
                Redis = false,
                Postgres = true,
                ServiceCode = "POS"
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