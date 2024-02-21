using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Services.Tenants;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ReceivedExtensions;

namespace Defra.Cdp.Backend.Api.Tests.Services.Aws.Deployments;

public class DeploymentEventsHandlerTests
{
    private readonly IDeploymentsService _deploymentsService = Substitute.For<IDeploymentsService>();
    private readonly IDeployablesService _deployablesService = Substitute.For<IDeployablesService>();
    private readonly ITestRunService _testRunService = Substitute.For<ITestRunService>();
    private readonly IEnvironmentLookup _environmentLookup = Substitute.For<IEnvironmentLookup>();
    readonly IOptions<EcsEventListenerOptions> cfg = Substitute.For<IOptions<EcsEventListenerOptions>>();
    
    private readonly DeploymentEventHandler handler;


    const string AccountId = "00000000";
    public DeploymentEventsHandlerTests()
    {

        _environmentLookup.FindEnv(AccountId).Returns("dev");
        cfg.Value.Returns(new EcsEventListenerOptions());
        
        handler = new DeploymentEventHandler(
            cfg,
            _environmentLookup,
            _deploymentsService,
            _deployablesService,
            _testRunService,
            ConsoleLogger.CreateLogger<DeploymentEventHandler>());
    }

    [Fact]
    public async void TestHandleUnlinkedTestSuiteMessage()
    {
        var name = "foo-test-suite";
        var now = DateTime.Now;
        var taskArn = "task-arn-1234";
        var ecsEvent = new EcsEvent(
            "deployment-id",
            "ECS Task State Change",
            AccountId,
            now,
            "eu-west-2",
            new EcsEventDetail(
                now, "1024", "1024",
                "RUNNING", "RUNNING",
                new() { new EcsContainer(name, "digest", "foo-test-suite", "RUNNING", "RUNNING") },
                "task-def-1234",
                taskArn,
                "reason",
                "started-by",
                null
            ), null, null);

        // Set up mocks
        var artifact = new DeployableArtifact { Repo = name, ServiceName = name, Tag = "0.1.0", RunMode = "job" };
        _testRunService.FindByTaskArn(taskArn, Arg.Any<CancellationToken>()).Returns(Task.FromResult<TestRun?>(null));
        _testRunService.Link(new TestRunMatchIds(name, "dev", now), taskArn, Arg.Any<CancellationToken>())
            .Returns(new TestRun()
            {
                Created = now.Subtract(TimeSpan.FromSeconds(10)) , Environment = "dev" , User = new UserDetails(), RunId = "1234"
            });
        _testRunService.UpdateStatus(taskArn, "in-progress", now, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        
        // Run the method
        await handler.UpdateTestSuite(ecsEvent, artifact, new CancellationToken());

        // Validate mock calls
        await _testRunService.Received().FindByTaskArn(taskArn, Arg.Any<CancellationToken>());
        await _testRunService.Received()
            .Link(new TestRunMatchIds(name, "dev", now), taskArn, Arg.Any<CancellationToken>());
        await _testRunService.Received().UpdateStatus(taskArn, "in-progress", now, Arg.Any<CancellationToken>());
    }
    
    
    [Fact]
    public async void TestHandleLinkedTestSuiteMessage()
    {
        var name = "foo-test-suite";
        var now = DateTime.Now;
        var taskArn = "task-arn-1234";
        var ecsEvent = new EcsEvent(
            "deployment-id",
            "ECS Task State Change",
            AccountId,
            now,
            "eu-west-2",
            new EcsEventDetail(
                now, "1024", "1024",
                "RUNNING", "RUNNING",
                new() { new EcsContainer(name, "digest", "foo-test-suite", "RUNNING", "RUNNING") },
                "task-def-1234",
                taskArn,
                "reason",
                "started-by",
                null
            ), null, null);

        var artifact = new DeployableArtifact { Repo = name, ServiceName = name, Tag = "0.1.0", RunMode = "job" };

        _testRunService.FindByTaskArn(taskArn, Arg.Any<CancellationToken>()).Returns(new TestRun()
        {
            Created = now.Subtract(TimeSpan.FromSeconds(10)) , Environment = "dev" , User = new UserDetails(), RunId = "1234"
        });

        _testRunService.UpdateStatus(taskArn, "in-progress", now, Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        await handler.UpdateTestSuite(ecsEvent, artifact, new CancellationToken());
        
        // Check stubs were all called
        await _testRunService.Received().FindByTaskArn(taskArn, Arg.Any<CancellationToken>());
        await _testRunService.DidNotReceiveWithAnyArgs().Link(Arg.Any<TestRunMatchIds>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _testRunService.Received().UpdateStatus(taskArn, "in-progress", now, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async void TestUpdateDeployment()
    {
        var name = "foo-service";
        var now = DateTime.Now;
        var taskArn = "task-arn-9999";
        var startedBy = "ecs-svc/9379658711849380294";
        var cdpId = "9999-9999-9999";
        
        var artifact = new DeployableArtifact { Repo = name, ServiceName = name, Tag = "0.1.0", RunMode = "service" };
        var ecsEvent = new EcsEvent(
            "deployment-id",
            "ECS Task State Change",
            AccountId,
            now,
            "eu-west-2",
            new EcsEventDetail(
                now, "1024", "1024",
                "RUNNING", "RUNNING",
                new() { new EcsContainer($"docker/{artifact.Repo}:{artifact.Tag}", "digest", name, "RUNNING", "RUNNING") },
                "task-def-9999",
                taskArn,
                "reason",
                startedBy,
                cdpId
            ), startedBy, cdpId);

        // Set up the mocks
        _deploymentsService.FindDeploymentByEcsSvcDeploymentId(startedBy, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<Deployment?>(new Deployment
            {
                DeploymentId = cdpId,
                User = "user",
                UserId = "user-1234"
            }));

        _deploymentsService.Insert(Arg.Any<Deployment>(), Arg.Any<CancellationToken>()).Returns(Task.CompletedTask);
        await handler.UpdateDeployment(ecsEvent, artifact, new CancellationToken());

        await _deploymentsService.Received().FindDeploymentByEcsSvcDeploymentId(startedBy, Arg.Any<CancellationToken>());
        await _deploymentsService.Received().Insert(Arg.Any<Deployment>(), Arg.Any<CancellationToken>());
    }

    
}