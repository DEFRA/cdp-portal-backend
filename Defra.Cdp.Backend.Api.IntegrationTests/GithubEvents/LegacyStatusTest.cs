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
using Defra.Cdp.Backend.Api.Services.GithubEvents;
using Defra.Cdp.Backend.Api.Services.GithubEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Creator = Defra.Cdp.Backend.Api.Services.GithubEvents.Model.Creator;
using Status = Defra.Cdp.Backend.Api.Services.GithubEvents.Model.Status;
using EntityStatus = Defra.Cdp.Backend.Api.Services.Entities.Model.Status;
using Team = Defra.Cdp.Backend.Api.Services.GithubEvents.Model.Team;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.IntegrationTests.GithubEvents;

public class LegacyStatusTest(MongoIntegrationTest fixture) : ServiceTest(fixture)
{
    private readonly IOptions<GithubEventListenerOptions> _githubEventListenerOptions =
        Substitute.For<IOptions<GithubEventListenerOptions>>();

    private readonly IOptions<GithubOptions> _githubOptions = Substitute.For<IOptions<GithubOptions>>();

    private readonly IEntityStatusService _entityStatusService = Substitute.For<IEntityStatusService>();

    private readonly GithubOptions _opts = new()
    {
        Organisation = "DEFRA",
        Repos =
            new GithubReposOptions
            {
                CdpTfSvcInfra = "cdp-tf-svc-infra",
                CdpAppConfig = "cdp-app-config",
                CdpAppDeployments = "cdp-app-deployments",
                CdpCreateWorkflows = "cdp-create-workflows",
                CdpGrafanaSvc = "cdp-grafana-svc",
                CdpNginxUpstreams = "cdp-nginx-upstreams",
                CdpSquidProxy = "cdp-squid-proxy"
            },
        Workflows = new GithubWorkflowsOptions
        {
            CreateAppConfig = "create-service.yml",
            CreateNginxUpstreams = "create-service.yml",
            CreateSquidConfig = "create-service.yml",
            CreateDashboard = "create-service.yml",
            CreateMicroservice = "create_microservice.yml",
            CreateRepository = "create_repository.yml",
            CreateJourneyTestSuite = "create_journey_test_suite.yml",
            CreatePerfTestSuite = "create_perf_test_suite.yml",
            CreateTenantService = "create-service.yml",
            ApplyTenantService = "apply.yml",
            ManualApplyTenantService = "manual.yml",
            NotifyPortal = "notify-portal.yml"
        }
    };

    [Fact]
    public async Task GithubEventUpdatesLegacyStatus()
    {
        _githubOptions.Value.Returns(_opts);

        _githubEventListenerOptions.Value.Returns(new GithubEventListenerOptions { QueueUrl = "http://localhost" });

        var mongoFactory = new MongoDbClientFactory(Fixture.connectionString, "LegacyStatuses");

        var loggerFactory = new LoggerFactory();
        var legacyStatusService = new LegacyStatusService(mongoFactory, _githubOptions, loggerFactory);
        var entitiesService = new EntitiesService(mongoFactory, loggerFactory);
        _entityStatusService.UpdateOverallStatus(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var statusUpdateService = new StatusUpdateService(legacyStatusService, _entityStatusService, _githubOptions);

        var legacyStatus = new LegacyStatus
        {
            RepositoryName = "example-repo",
            Status = Status.InProgress.ToStringValue(),
            Team = new Team { TeamId = Guid.NewGuid().ToString(), Name = "example-team" },
            Kind = CreationType.Microservice.ToStringValue(),
            ServiceTypeTemplate = "example-template",
            Zone = "protected",
            Creator = new Creator
            {
                Id = Guid.NewGuid().ToString(),
                DisplayName = "example-creator"
            },
        };

        await legacyStatusService.Create(legacyStatus, CancellationToken.None);
        await entitiesService.Create(Entity.from(legacyStatus), CancellationToken.None);

        var persistedStatus = await legacyStatusService.StatusForRepositoryName("example-repo", CancellationToken.None);
        Assert.NotNull(persistedStatus);
        Assert.Equal("example-repo", persistedStatus.RepositoryName);
        Assert.Equal(Status.InProgress.ToStringValue(), persistedStatus.Status);
        Assert.Equal(CreationType.Microservice.ToStringValue(), persistedStatus.Kind);
        Assert.Equal("example-template", persistedStatus.ServiceTypeTemplate);
        Assert.Equal("example-team", persistedStatus.Team.Name);

        var persistedEntity = await entitiesService.GetEntity("example-repo", CancellationToken.None);
        Assert.NotNull(persistedEntity);
        Assert.Equal("example-repo", persistedEntity.Name);
        Assert.Equal(EntityStatus.Creating, persistedEntity.Status);
        Assert.Equal(Type.Microservice, persistedEntity.Type);
        Assert.Equal(SubType.Backend, persistedEntity.SubType);
        Assert.Equal("example-team", persistedEntity.Teams[0].Name);
        Assert.Equal("example-creator", persistedEntity.Creator!.Name);
        Assert.NotNull(persistedEntity.Created);

        var sqs = Substitute.For<IAmazonSQS>();
        var tenantServicesService = new TenantServicesService(mongoFactory,
            new RepositoryService(mongoFactory, loggerFactory), loggerFactory);
        var githubEventHandler = new GithubEventHandler(legacyStatusService,
            statusUpdateService,
            tenantServicesService,
            new DeployableArtifactsService(mongoFactory, loggerFactory),
            _githubOptions,
            loggerFactory.CreateLogger<GithubEventHandler>()
        );

        var githubEventListener = new GithubEventListener(
            sqs,
            _githubEventListenerOptions,
            _githubOptions,
            githubEventHandler,
            loggerFactory.CreateLogger<GithubEventListener>());

        await githubEventListener.Handle(
            new Message
            {
                Body = GetBody(
                "example-repo",
                "cdp-create-workflows",
                "create_microservice.yml"
                ),
                MessageId = "1234"
            },
            CancellationToken.None);

        var updatedStatus = await legacyStatusService.StatusForRepositoryName("example-repo", CancellationToken.None);

        Assert.NotNull(updatedStatus);
        Assert.Equal(Status.InProgress.ToStringValue(), updatedStatus.Status);
        Assert.Equal(Status.Requested.ToStringValue(), updatedStatus.CdpCreateWorkflows.Status);

        var updatedEntity = await entitiesService.GetEntity("example-repo", CancellationToken.None);
        Assert.NotNull(updatedEntity);
        Assert.Equal(EntityStatus.Creating, updatedEntity.Status);

        await githubEventListener.Handle(
            new Message
            {
                Body = GetBody(
                "example-repo",
                "cdp-create-workflows",
                "create_microservice.yml",
                Status.Completed,
                Status.Success
                ),
                MessageId = "1234"
            },
            CancellationToken.None);

        updatedStatus = await legacyStatusService.StatusForRepositoryName("example-repo", CancellationToken.None);
        Assert.NotNull(updatedStatus);
        Assert.Equal(Status.InProgress.ToStringValue(), updatedStatus.Status);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.CdpCreateWorkflows.Status);

        updatedEntity = await entitiesService.GetEntity("example-repo", CancellationToken.None);
        Assert.NotNull(updatedEntity);
        Assert.Equal(EntityStatus.Creating, updatedEntity.Status);

        await githubEventListener.Handle(
            new Message
            {
                Body = GetBody(
                "example-repo",
                "cdp-nginx-upstreams",
                "create-service.yml",
                Status.Completed,
                Status.Success
            ),
                MessageId = "1234"
            },
            CancellationToken.None);

        await githubEventListener.Handle(
            new Message
            {
                Body = GetBody(
                "example-repo",
                "cdp-app-config",
                "create-service.yml",
                Status.Completed,
                Status.Success
            ),
                MessageId = "1234"
            },
            CancellationToken.None);
        await githubEventListener.Handle(
            new Message
            {
                Body = GetBody(
                "example-repo",
                "cdp-squid-proxy",
                "create-service.yml",
                Status.Completed,
                Status.Success
            ),
                MessageId = "1234"
            },
            CancellationToken.None);

        await githubEventListener.Handle(
            new Message
            {
                Body = GetBody(
                "example-repo",
                "cdp-grafana-svc",
                "create-service.yml",
                Status.Completed,
                Status.Success
            ),
                MessageId = "1234"
            },
            CancellationToken.None);


        await tenantServicesService.PersistEvent(new CommonEvent<TenantServicesPayload>
        {
            EventType = "create",
            Payload = new TenantServicesPayload
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
            },
            Timestamp = DateTime.Now
        }, CancellationToken.None);

        await githubEventListener.Handle(
            new Message
            {
                Body = GetBody(
                "example-repo",
                "cdp-tf-svc-infra",
                "notify-portal.yml",
                Status.Completed,
                Status.Success
            ),
                MessageId = "1234"
            },
            CancellationToken.None);


        updatedStatus = await legacyStatusService.StatusForRepositoryName("example-repo", CancellationToken.None);
        Assert.NotNull(updatedStatus);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.CdpCreateWorkflows.Status);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.CdpNginxUpstreams?.Status);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.CdpAppConfig?.Status);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.CdpGrafanaSvc?.Status);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.CdpTfSvcInfra?.Status);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.CdpSquidProxy?.Status);
        Assert.Equal(Status.Success.ToStringValue(), updatedStatus.Status);
    }


    private static string GetBody(
        string serviceRepo,
        string workflowRepo = "cdp-tf-svc-infra",
        string workflowFile = "create-service.yml",
        Status action = Status.Requested,
        Status conclusion = Status.Requested,
        string eventType = "workflow_run")
    {
        return $@"{{
                  ""github_event"": ""{eventType}"",
                  ""action"": ""{action.ToStringValue()}"",
                  ""workflow_run"": {{
                    ""head_sha"": ""f1d2d2f924e986ac86fdf7b36c94bcdf32beec15"",
                    ""head_branch"": ""main"",
                    ""name"": ""{serviceRepo}"",
                    ""id"": 1,
                    ""conclusion"": ""{conclusion.ToStringValue()}"",
                    ""html_url"": ""http://localhost:3939/#local-stub"",
                    ""created_at"": ""2025-03-31T13:29:36.987Z"",
                    ""updated_at"": ""2025-03-31T13:29:36.987Z"",
                    ""path"": "".github/workflows/{workflowFile}"",
                    ""run_number"": 1,
                    ""head_commit"": {{
                      ""message"": ""commit message"",
                      ""author"": {{
                        ""name"": ""stub""
                      }}
                    }}
                  }},
                  ""repository"": {{
                    ""name"": ""{workflowRepo}"",
                    ""html_url"": ""http://localhost:3939/#local-stub""
                  }},
                  ""workflow"": {{
                    ""path"": "".github/workflows/{workflowFile}""
                  }}
                }}";
    }
}