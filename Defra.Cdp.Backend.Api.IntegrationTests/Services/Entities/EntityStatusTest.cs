using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.IntegrationTests.Mongo;
using Defra.Cdp.Backend.Api.IntegrationTests.Utils;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Entities;

public class EntityStatusTest(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    private readonly IOptions<GithubWorkflowEventListenerOptions> _githubWorkflowEventListenerOptions =
        Substitute.For<IOptions<GithubWorkflowEventListenerOptions>>();

    [Fact]
    public async Task NotifyPortalUpdatesEntityStatus()
    {
        _githubWorkflowEventListenerOptions.Value.Returns(
            new GithubWorkflowEventListenerOptions { QueueUrl = "http://localhost" });

        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "EntityStatusTest");

        var loggerFactory = new LoggerFactory();
        var envLookup = new MockEnvironmentLookup();
        var entitiesService = new EntitiesService(mongoFactory, loggerFactory);
        var repositoryService = new RepositoryService(mongoFactory, loggerFactory);
        var tenantServicesService = new TenantServicesService(mongoFactory, repositoryService, envLookup, loggerFactory);
        var squidProxyService = new SquidProxyConfigService(mongoFactory, loggerFactory);
        var nginxUpstreamsService = new NginxUpstreamsService(mongoFactory, loggerFactory);
        var appConfigService = new AppConfigsService(mongoFactory, loggerFactory);
        var appConfigVersionsService = new AppConfigVersionsService(mongoFactory, loggerFactory);
        var grafanaDashboardsService = new GrafanaDashboardsService(mongoFactory, loggerFactory);
        var entityStatusService = new EntityStatusService(entitiesService,
            repositoryService,
            tenantServicesService,
            squidProxyService,
            nginxUpstreamsService,
            appConfigService,
            grafanaDashboardsService,
            loggerFactory.CreateLogger<EntityStatusService>());
        var tenantRdsService = new TenantRdsDatabasesService(mongoFactory, loggerFactory);

        var entity = new Entity
        {
            Name = "example-repo",
            Status = Status.Creating,
            Teams = [new Team { TeamId = Guid.NewGuid().ToString(), Name = "example-team" }],
            Type = Type.Microservice,
            SubType = SubType.Backend,
            Creator = new UserDetails { Id = Guid.NewGuid().ToString(), DisplayName = "example-creator" },
            Created = DateTime.UtcNow
        };

        await entitiesService.Create(entity, CancellationToken.None);

        var persistedEntityStatus = await entityStatusService.GetEntityStatus("example-repo", CancellationToken.None);
        var persistedEntity = persistedEntityStatus?.Entity;
        Assert.NotNull(persistedEntity);
        Assert.Equal("example-repo", persistedEntity.Name);
        Assert.Equal(Status.Creating, persistedEntity.Status);
        Assert.Equal(Type.Microservice, persistedEntity.Type);
        Assert.Equal(SubType.Backend, persistedEntity.SubType);
        Assert.Equal("example-team", persistedEntity.Teams[0].Name);
        Assert.Equal("example-creator", persistedEntity.Creator!.DisplayName);
        Assert.NotNull(persistedEntity.Created);
        Assert.Equal(Status.Creating, persistedEntity.Status);

        Assert.False(persistedEntityStatus.Resources["Repository"]);
        Assert.False(persistedEntityStatus.Resources["TenantServices"]);
        Assert.False(persistedEntityStatus.Resources["SquidProxy"]);
        Assert.False(persistedEntityStatus.Resources["NginxUpstreams"]);
        Assert.False(persistedEntityStatus.Resources["AppConfig"]);
        Assert.False(persistedEntityStatus.Resources["GrafanaDashboard"]);

        var sqs = Substitute.For<IAmazonSQS>();

        var githubWorkflowEventHandler = new GithubWorkflowEventHandler(
            appConfigVersionsService,
            appConfigService,
            new NginxVanityUrlsService(mongoFactory, loggerFactory),
            squidProxyService,
            tenantServicesService,
            new ShutteredUrlsService(mongoFactory, loggerFactory),
            new EnabledVanityUrlsService(mongoFactory, loggerFactory),
            new EnabledApisService(mongoFactory, loggerFactory),
            new TfVanityUrlsService(mongoFactory, loggerFactory),
            grafanaDashboardsService,
            nginxUpstreamsService,
            entityStatusService,
            tenantRdsService,
            loggerFactory.CreateLogger<GithubWorkflowEventHandler>()
        );

        await repositoryService.Upsert(new Repository
        {
            Id = "example-repo",
            IsTemplate = false,
            Topics = new List<string> { "cdp" },
            Teams = new List<RepositoryTeam>
            {
                new(
                    "example-github-team",
                    Guid.NewGuid().ToString(),
                    "example-team"
                )
            },
            CreatedAt = DateTime.UtcNow,
        }, CancellationToken.None);

        var updatedEntity = await entityStatusService.GetEntityStatus("example-repo", CancellationToken.None);

        Assert.NotNull(updatedEntity);
        Assert.Equal(Status.Creating, updatedEntity.Entity.Status);
        Assert.True(updatedEntity.Resources["Repository"]);
        Assert.False(updatedEntity.Resources["TenantServices"]);
        Assert.False(updatedEntity.Resources["SquidProxy"]);
        Assert.False(updatedEntity.Resources["NginxUpstreams"]);
        Assert.False(updatedEntity.Resources["AppConfig"]);
        Assert.False(updatedEntity.Resources["GrafanaDashboard"]);


        var githubWorkflowEventListener = new GithubWorkflowEventListener(
            sqs,
            _githubWorkflowEventListenerOptions,
            githubWorkflowEventHandler,
            loggerFactory.CreateLogger<GithubWorkflowEventListener>());

        var tenantServicesPayload = new TenantServicesPayload
        {
            Environment = "management",
            Services =
            [
                new Service
                {
                    Name = "example-repo",
                    Zone = "public",
                    Mongo = true,
                    Redis = false,
                    ServiceCode = "example-repo",
                    TestSuite = null,
                }
            ]
        };

        await githubWorkflowEventListener.Handle(
            new Message { Body = WrapBodyAsJson(tenantServicesPayload, "tenant-services"), MessageId = "1234" },
            CancellationToken.None);

        updatedEntity = await entityStatusService.GetEntityStatus("example-repo", CancellationToken.None);
        Assert.NotNull(updatedEntity);
        Assert.Equal(Status.Creating, updatedEntity.Entity.Status);
        Assert.True(updatedEntity.Resources["Repository"]);
        Assert.True(updatedEntity.Resources["TenantServices"]);
        Assert.False(updatedEntity.Resources["SquidProxy"]);
        Assert.False(updatedEntity.Resources["NginxUpstreams"]);
        Assert.False(updatedEntity.Resources["AppConfig"]);
        Assert.False(updatedEntity.Resources["GrafanaDashboard"]);

        var nginxUpstreamsPayload = new NginxUpstreamsPayload
        {
            Environment = "management",
            Entities =
            [
                "example-repo"
            ]
        };

        await githubWorkflowEventListener.Handle(
            new Message { Body = WrapBodyAsJson(nginxUpstreamsPayload, "nginx-upstreams"), MessageId = "1234" },
            CancellationToken.None);

        updatedEntity = await entityStatusService.GetEntityStatus("example-repo", CancellationToken.None);
        Assert.NotNull(updatedEntity);
        Assert.Equal(Status.Creating, updatedEntity.Entity.Status);
        Assert.True(updatedEntity.Resources["Repository"]);
        Assert.True(updatedEntity.Resources["TenantServices"]);
        Assert.False(updatedEntity.Resources["SquidProxy"]);
        Assert.True(updatedEntity.Resources["NginxUpstreams"]);
        Assert.False(updatedEntity.Resources["AppConfig"]);
        Assert.False(updatedEntity.Resources["GrafanaDashboard"]);

        var appConfigPayload = new AppConfigPayload
        {
            Environment = "management",
            CommitSha = "abcd1234",
            CommitTimestamp = DateTime.UtcNow,
            Entities =
            [
                "example-repo"
            ]
        };

        await githubWorkflowEventListener.Handle(
            new Message { Body = WrapBodyAsJson(appConfigPayload, "app-config"), MessageId = "1234" },
            CancellationToken.None);

        updatedEntity = await entityStatusService.GetEntityStatus("example-repo", CancellationToken.None);
        Assert.NotNull(updatedEntity);
        Assert.Equal(Status.Creating, updatedEntity.Entity.Status);
        Assert.True(updatedEntity.Resources["Repository"]);
        Assert.True(updatedEntity.Resources["TenantServices"]);
        Assert.False(updatedEntity.Resources["SquidProxy"]);
        Assert.True(updatedEntity.Resources["NginxUpstreams"]);
        Assert.True(updatedEntity.Resources["AppConfig"]);
        Assert.False(updatedEntity.Resources["GrafanaDashboard"]);

        var squidProxyConfig = new SquidProxyConfigPayload
        {
            Environment = "management",
            DefaultDomains = [],
            Services =
            [
                new ServiceConfig { AllowedDomains = [], Name = "example-repo", }
            ]
        };

        await githubWorkflowEventListener.Handle(
            new Message { Body = WrapBodyAsJson(squidProxyConfig, "squid-proxy-config"), MessageId = "1234" },
            CancellationToken.None);

        updatedEntity = await entityStatusService.GetEntityStatus("example-repo", CancellationToken.None);
        Assert.NotNull(updatedEntity);
        Assert.Equal(Status.Creating, updatedEntity.Entity.Status);
        Assert.True(updatedEntity.Resources["Repository"]);
        Assert.True(updatedEntity.Resources["TenantServices"]);
        Assert.True(updatedEntity.Resources["SquidProxy"]);
        Assert.True(updatedEntity.Resources["NginxUpstreams"]);
        Assert.True(updatedEntity.Resources["AppConfig"]);
        Assert.False(updatedEntity.Resources["GrafanaDashboard"]);

        var grafanaDashboardPayload = new GrafanaDashboardPayload
        {
            Environment = "management",
            Entities =
            [
                "example-repo"
            ]
        };
        await githubWorkflowEventListener.Handle(
            new Message { Body = WrapBodyAsJson(grafanaDashboardPayload, "grafana-dashboard"), MessageId = "1234" },
            CancellationToken.None);

        updatedEntity = await entityStatusService.GetEntityStatus("example-repo", CancellationToken.None);
        Assert.NotNull(updatedEntity);
        Assert.True(updatedEntity.Resources["Repository"]);
        Assert.True(updatedEntity.Resources["TenantServices"]);
        Assert.True(updatedEntity.Resources["SquidProxy"]);
        Assert.True(updatedEntity.Resources["NginxUpstreams"]);
        Assert.True(updatedEntity.Resources["AppConfig"]);
        Assert.True(updatedEntity.Resources["GrafanaDashboard"]);
        Assert.Equal(Status.Created, updatedEntity.Entity.Status);
    }

    private static string WrapBodyAsJson<T>(T tenantServicesPayload, string eventType)
    {
        var wrapper = new CommonEvent<T>
        {
            EventType = eventType,
            Timestamp = DateTime.Now,
            Payload = tenantServicesPayload
        };

        return JsonSerializer.Serialize(wrapper);
    }
}