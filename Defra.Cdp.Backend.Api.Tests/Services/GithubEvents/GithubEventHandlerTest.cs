using System.Text.Json;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.GithubEvents;
using Defra.Cdp.Backend.Api.Services.GithubEvents.Model;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Defra.Cdp.Backend.Api.Tests.Services.GithubEvents;

public class GithubEventHandlerTest
{
    private readonly ILegacyStatusService _legacyStatusService = Substitute.For<ILegacyStatusService>();
    private readonly IStatusUpdateService _statusUpdateService = Substitute.For<IStatusUpdateService>();
    private readonly ITenantServicesService _tenantServicesService = Substitute.For<ITenantServicesService>();

    private readonly IDeployableArtifactsService _deployableArtifactsService =
        Substitute.For<IDeployableArtifactsService>();

    private readonly IOptions<GithubOptions> _githubOptions = Substitute.For<IOptions<GithubOptions>>();

    private readonly GithubOptions _opts = new()
    {
        Organisation = "DEFRA",
        Repos = new GithubReposOptions
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
    public async Task ShouldNotProcessWorkflowsNotOnMainBranch()
    {
        var githubEventHandler = new GithubEventHandler(_legacyStatusService, _statusUpdateService, _tenantServicesService,
            _deployableArtifactsService, _githubOptions, new LoggerFactory().CreateLogger<GithubEventHandler>());

        var msg = """
                    {
                      "github_event" : "workflow_run",
                      "action" : "completed",
                      "workflow_run" : {
                          "head_branch" : "not-main",
                          "head_sha" : "6d96270004515a0486bb7f76196a72b40c55a47f",
                          "path" : ".github/workflows/create-service.yml"
                      },
                      "repository" : { "name" : "tf-svc", "owner" :  { "login" : "test-org" } }
                    }
                  """;

        var message = JsonSerializer.Deserialize<GithubEventMessage>(msg)!;

        await githubEventHandler.Handle(message, CancellationToken.None);

        await _legacyStatusService.DidNotReceive().UpdateWorkflowStatus(Arg.Any<string>(), Arg.Any<string>(),
            Arg.Any<string>(), Arg.Any<Status>(), Arg.Any<TrimmedWorkflowRun>());
        await _statusUpdateService.DidNotReceive().UpdateOverallStatus(Arg.Any<string>(), CancellationToken.None);
        await _legacyStatusService.DidNotReceive().StatusForRepositoryName(Arg.Any<string>(), CancellationToken.None);
        await _tenantServicesService.DidNotReceive().FindOne(Arg.Any<TenantServiceFilter>(), CancellationToken.None);
        await _deployableArtifactsService.DidNotReceive()
            .CreatePlaceholderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ArtifactRunMode>(),
                CancellationToken.None);
    }

    [Fact]
    public async Task CallHandleTriggeredWorkflowForValidRepoNotCdpTfSvcInfra()
    {
        var githubEventHandler = new GithubEventHandler(_legacyStatusService, _statusUpdateService, _tenantServicesService,
            _deployableArtifactsService, _githubOptions, new LoggerFactory().CreateLogger<GithubEventHandler>());

        _githubOptions.Value.Returns(_opts);

        var msg = """
                  {
                      "github_event" : "workflow_run",
                      "action" : "completed",
                      "workflow_run" : 
                      {
                          "id" : 999,
                          "name" : "wf-name",
                          "html_url" : "http://localhost",
                          "created_at" : "2025-04-01T13:45:27.041Z",
                          "updated_at" : "2025-04-01T13:45:27.041Z",
                          "path" : ".github/workflows/create-service.yml",
                          "head_branch" : "main",
                          "head_sha" : "6d96270004515a0486bb7f76196a72b40c55a47f",
                          "conclusion" : "failure"
                      },
                      "repository" :  { "name" : "cdp-app-config", "owner" :  { "login" : "test-org" } }
                  }
                  """;
        var message = JsonSerializer.Deserialize<GithubEventMessage>(msg)!;

        _legacyStatusService.StatusForRepositoryName("wf-name", CancellationToken.None).Returns(
            new LegacyStatus { RepositoryName = "wf-name", Status = Status.InProgress.ToStringValue() }
        );

        await githubEventHandler.Handle(message, CancellationToken.None);

        await _legacyStatusService.Received(1).StatusForRepositoryName("wf-name", CancellationToken.None);
        await _legacyStatusService.Received(1).UpdateWorkflowStatus("wf-name", "cdp-app-config",
            Arg.Any<string>(), Arg.Any<Status>(), Arg.Any<TrimmedWorkflowRun>());
        await _statusUpdateService.Received(1).UpdateOverallStatus("wf-name", CancellationToken.None);
        await _legacyStatusService.DidNotReceive().FindAllInProgressOrFailed(CancellationToken.None);
        await _tenantServicesService.DidNotReceive().FindOne(Arg.Any<TenantServiceFilter>(), CancellationToken.None);
        await _deployableArtifactsService.DidNotReceive()
            .CreatePlaceholderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ArtifactRunMode>(),
                CancellationToken.None);
    }

    [Fact]
    public async Task DontUpdateAnythingForTfSvcInfraIfCreateWorkflowAndItsCompleted()
    {
        var githubEventHandler = new GithubEventHandler(_legacyStatusService,_statusUpdateService, _tenantServicesService,
            _deployableArtifactsService, _githubOptions, new LoggerFactory().CreateLogger<GithubEventHandler>());

        _githubOptions.Value.Returns(_opts);
        var msg = """
                  {
                      "github_event" : "workflow_run",
                      "action" : "completed",
                      "workflow_run" : 
                      {
                          "id" : 999,
                          "name" : "wf-name",
                          "html_url" : "http://localhost",
                          "created_at" : "2025-04-01T13:45:27.041Z",
                          "updated_at" : "2025-04-01T13:45:27.041Z",
                          "path" : ".github/workflows/create-service.yml",
                          "head_branch" : "main",
                          "head_sha" : "6d96270004515a0486bb7f76196a72b40c55a47f",
                          "conclusion" : "failure"
                      },
                      "repository" :  { "name" : "cdp-tf-svc-infra", "owner" :  { "login" : "test-org" } }
                  }
                  """;
        var message = JsonSerializer.Deserialize<GithubEventMessage>(msg)!;

        await githubEventHandler.Handle(message, CancellationToken.None);

        await _legacyStatusService.DidNotReceive().StatusForRepositoryName("wf-name", CancellationToken.None);
        await _legacyStatusService.DidNotReceive().UpdateWorkflowStatus("wf-name", "cdp-app-config",
            Arg.Any<string>(), Arg.Any<Status>(), Arg.Any<TrimmedWorkflowRun>());
        await _statusUpdateService.DidNotReceive().UpdateOverallStatus("wf-name", CancellationToken.None);
        await _legacyStatusService.DidNotReceive().FindAllInProgressOrFailed(CancellationToken.None);
        await _tenantServicesService.DidNotReceive().FindOne(Arg.Any<TenantServiceFilter>(), CancellationToken.None);
        await _deployableArtifactsService.DidNotReceive()
            .CreatePlaceholderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ArtifactRunMode>(),
                CancellationToken.None);
    }

    [Fact]
    public async Task NotProcessAnythingForUnsupportedRepo()
    {
        var githubEventHandler = new GithubEventHandler(_legacyStatusService,_statusUpdateService, _tenantServicesService,
            _deployableArtifactsService, _githubOptions, new LoggerFactory().CreateLogger<GithubEventHandler>());

        _githubOptions.Value.Returns(_opts);
        var msg = """
                  {
                      "github_event" : "workflow_run",
                      "action" : "in-progress",
                      "workflow_run" : 
                      {
                          "id" : 999,
                          "name" : "wf-name",
                          "html_url" : "http://localhost",
                          "created_at" : "2025-04-01T13:45:27.041Z",
                          "updated_at" : "2025-04-01T13:45:27.041Z",
                          "path" : ".github/workflows/create-service.yml",
                          "head_branch" : "main",
                          "head_sha" : "6d96270004515a0486bb7f76196a72b40c55a47f",
                          "conclusion" : "failure"
                      },
                      "repository" :  { "name" : "cdp-portal-frontend", "owner" :  { "login" : "test-org" } }
                  }
                  """;
        var message = JsonSerializer.Deserialize<GithubEventMessage>(msg)!;

        await githubEventHandler.Handle(message, CancellationToken.None);

        await _legacyStatusService.DidNotReceive().StatusForRepositoryName("wf-name", CancellationToken.None);
        await _legacyStatusService.DidNotReceive().UpdateWorkflowStatus("wf-name", "cdp-app-config",
            Arg.Any<string>(), Arg.Any<Status>(), Arg.Any<TrimmedWorkflowRun>());
        await _statusUpdateService.DidNotReceive().UpdateOverallStatus("wf-name", CancellationToken.None);
        await _legacyStatusService.DidNotReceive().FindAllInProgressOrFailed(CancellationToken.None);
        await _tenantServicesService.DidNotReceive().FindOne(Arg.Any<TenantServiceFilter>(), CancellationToken.None);
        await _deployableArtifactsService.DidNotReceive()
            .CreatePlaceholderAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<ArtifactRunMode>(),
                CancellationToken.None);
    }

    [Fact]
    public async Task UpdatePendingServiceStatusThatHasTenantJsonEntry()
    {
        var githubEventHandler = new GithubEventHandler(_legacyStatusService, _statusUpdateService, _tenantServicesService,
            _deployableArtifactsService, _githubOptions, new LoggerFactory().CreateLogger<GithubEventHandler>());

        _githubOptions.Value.Returns(_opts);
        var msg = """
                  {
                      "github_event" : "workflow_run",
                      "action" : "completed",
                      "workflow_run" : 
                      {
                          "id" : 999,
                          "name" : "wf-name",
                          "html_url" : "http://localhost",
                          "created_at" : "2025-04-01T13:45:27.041Z",
                          "updated_at" : "2025-04-01T13:45:27.041Z",
                          "path" : ".github/workflows/manual.yml",
                          "head_branch" : "main",
                          "head_sha" : "6d96270004515a0486bb7f76196a72b40c55a47f",
                          "conclusion" : "success"
                      },
                      "repository" :  { "name" : "cdp-tf-svc-infra", "owner" :  { "login" : "test-org" } }
                  }
                  """;

        _legacyStatusService.StatusForRepositoryName("wf-name", CancellationToken.None).Returns(
            new LegacyStatus { RepositoryName = "wf-name", Status = Status.InProgress.ToStringValue() }
        );

        _legacyStatusService.FindAllInProgressOrFailed(CancellationToken.None).Returns([
                new LegacyStatus { RepositoryName = "wf-name", Status = Status.InProgress.ToStringValue() }
            ]
        );
        _tenantServicesService.FindOne(Arg.Any<TenantServiceFilter>(), CancellationToken.None).Returns(
            new TenantServiceRecord
            (
                "management",
                "wf-name",
                "Public",
                true,
                false,
                false,
                "abc123",
                null,
                null,
                null,
                null,
                null,
                [new RepositoryTeam("something", "team-id", "team-name")]
            ));


        var message = JsonSerializer.Deserialize<GithubEventMessage>(msg)!;

        await githubEventHandler.Handle(message, CancellationToken.None);

        await _legacyStatusService.DidNotReceive().StatusForRepositoryName("wf-name", CancellationToken.None);
        await _legacyStatusService.Received(1).UpdateWorkflowStatus("wf-name", "cdp-tf-svc-infra",
            "main", Status.Success, Arg.Any<TrimmedWorkflowRun>());
        await _statusUpdateService.Received(1).UpdateOverallStatus("wf-name", CancellationToken.None);
        await _legacyStatusService.Received(1).FindAllInProgressOrFailed(CancellationToken.None);
        await _tenantServicesService.Received(1).FindOne(Arg.Any<TenantServiceFilter>(), CancellationToken.None);
        await _deployableArtifactsService.Received(1)
            .CreatePlaceholderAsync("wf-name", "https://github.com/DEFRA/wf-name", ArtifactRunMode.Service,
                CancellationToken.None);
    }

    [Fact]
    public async Task DoNotUpdatePendingServiceStatusThatHasNoTenantJsonEntry()
    {
        var githubEventHandler = new GithubEventHandler(_legacyStatusService, _statusUpdateService, _tenantServicesService,
            _deployableArtifactsService, _githubOptions, new LoggerFactory().CreateLogger<GithubEventHandler>());

        _githubOptions.Value.Returns(_opts);
        var msg = """
                  {
                      "github_event" : "workflow_run",
                      "action" : "completed",
                      "workflow_run" : 
                      {
                          "id" : 999,
                          "name" : "wf-name",
                          "html_url" : "http://localhost",
                          "created_at" : "2025-04-01T13:45:27.041Z",
                          "updated_at" : "2025-04-01T13:45:27.041Z",
                          "path" : ".github/workflows/manual.yml",
                          "head_branch" : "main",
                          "head_sha" : "6d96270004515a0486bb7f76196a72b40c55a47f",
                          "conclusion" : "success"
                      },
                      "repository" :  { "name" : "cdp-tf-svc-infra", "owner" :  { "login" : "test-org" } }
                  }
                  """;

        _legacyStatusService.FindAllInProgressOrFailed(CancellationToken.None).Returns([
                new LegacyStatus { RepositoryName = "wf-name", Status = Status.InProgress.ToStringValue() }
            ]
        );
        _tenantServicesService.FindOne(Arg.Any<TenantServiceFilter>(), CancellationToken.None).ReturnsNull();

        var message = JsonSerializer.Deserialize<GithubEventMessage>(msg)!;

        await githubEventHandler.Handle(message, CancellationToken.None);

        await _legacyStatusService.DidNotReceive().StatusForRepositoryName("wf-name", CancellationToken.None);
        await _legacyStatusService.DidNotReceive().UpdateWorkflowStatus("wf-name", "cdp-tf-svc-infra",
            "main", Status.Success, Arg.Any<TrimmedWorkflowRun>());
        await _statusUpdateService.DidNotReceive().UpdateOverallStatus("wf-name", CancellationToken.None);
        await _legacyStatusService.Received(1).FindAllInProgressOrFailed(CancellationToken.None);
        await _tenantServicesService.Received(1).FindOne(Arg.Any<TenantServiceFilter>(), CancellationToken.None);
        await _deployableArtifactsService.DidNotReceive()
            .CreatePlaceholderAsync("wf-name", "https://github.com/DEFRA/wf-name", ArtifactRunMode.Service,
                CancellationToken.None);
    }


    [Fact]
    public async Task UpdatePendingTestSuiteStatusThatHasTenantJsonEntry()
    {
        var githubEventHandler = new GithubEventHandler(_legacyStatusService,_statusUpdateService, _tenantServicesService,
            _deployableArtifactsService, _githubOptions, new LoggerFactory().CreateLogger<GithubEventHandler>());

        _githubOptions.Value.Returns(_opts);
        var msg = """
                  {
                      "github_event" : "workflow_run",
                      "action" : "completed",
                      "workflow_run" : 
                      {
                          "id" : 999,
                          "name" : "a-test-suite",
                          "html_url" : "http://localhost",
                          "created_at" : "2025-04-01T13:45:27.041Z",
                          "updated_at" : "2025-04-01T13:45:27.041Z",
                          "path" : ".github/workflows/manual.yml",
                          "head_branch" : "main",
                          "head_sha" : "6d96270004515a0486bb7f76196a72b40c55a47f",
                          "conclusion" : "success"
                      },
                      "repository" :  { "name" : "cdp-tf-svc-infra", "owner" :  { "login" : "test-org" } }
                  }
                  """;

        _legacyStatusService.StatusForRepositoryName("a-test-suite", CancellationToken.None).Returns(
            new LegacyStatus { RepositoryName = "a-test-suite", Status = Status.InProgress.ToStringValue() }
        );

        _legacyStatusService.FindAllInProgressOrFailed(CancellationToken.None).Returns([
                new LegacyStatus { RepositoryName = "a-test-suite", Status = Status.InProgress.ToStringValue() }
            ]
        );
        _tenantServicesService.FindOne(Arg.Any<TenantServiceFilter>(), CancellationToken.None).Returns(
            new TenantServiceRecord
            (
                "management",
                "a-test-suite",
                "Public",
                true,
                false,
                false,
                "abc123",
                "a-test-suite",
                null,
                null,
                null,
                null,
                [new RepositoryTeam("something", "team-id", "team-name")]
            ));


        var message = JsonSerializer.Deserialize<GithubEventMessage>(msg)!;

        await githubEventHandler.Handle(message, CancellationToken.None);

        await _legacyStatusService.DidNotReceive().StatusForRepositoryName("a-test-suite", CancellationToken.None);
        await _legacyStatusService.Received(1).UpdateWorkflowStatus("a-test-suite", "cdp-tf-svc-infra",
            "main", Status.Success, Arg.Any<TrimmedWorkflowRun>());
        await _statusUpdateService.Received(1).UpdateOverallStatus("a-test-suite", CancellationToken.None);
        await _legacyStatusService.Received(1).FindAllInProgressOrFailed(CancellationToken.None);
        await _tenantServicesService.Received(1).FindOne(Arg.Any<TenantServiceFilter>(), CancellationToken.None);
        await _deployableArtifactsService.Received(1)
            .CreatePlaceholderAsync("a-test-suite", "https://github.com/DEFRA/a-test-suite", ArtifactRunMode.Job,
                CancellationToken.None);
    }
}