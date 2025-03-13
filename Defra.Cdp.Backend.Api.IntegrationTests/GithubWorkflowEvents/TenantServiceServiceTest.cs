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
        var resultFoo = await tenantServicesService.FindService("foo", "test", CancellationToken.None);
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

        var updatedResults = await tenantServicesService.FindService("foo", "test", CancellationToken.None);
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

        var resultFoo = await tenantServicesService.FindService("foo", "test", CancellationToken.None);
        Assert.Equivalent(resultFoo?.Teams, new List<RepositoryTeam> { new("foo-team", "1234", "foo-team") });

        var resultBar = await tenantServicesService.FindService("bar", "test", CancellationToken.None);

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

        var resultFoo = await tenantServicesService.FindService("foo", "test", CancellationToken.None);
        var resultBar = await tenantServicesService.FindService("bar", "test", CancellationToken.None);
        Assert.NotNull(resultFoo);
        Assert.NotNull(resultBar);

        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
            {
                EventType = "tenant-services",
                Timestamp = DateTime.Now,
                Payload = new() { Environment = "test", Services = [_sampleEvent.Services[0]] }
            }
            , CancellationToken.None);
        resultFoo = await tenantServicesService.FindService("foo", "test", CancellationToken.None);
        resultBar = await tenantServicesService.FindService("bar", "test", CancellationToken.None);
        Assert.NotNull(resultFoo);
        Assert.Null(resultBar);
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
                ServiceCode = "bar"
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
}