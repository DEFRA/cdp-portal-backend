using System.Text.Json;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Defra.Cdp.Backend.Api.Tests.Services.Aws.Deployments;

class MockEnvironmentLookup : IEnvironmentLookup
{
    public string? FindEnv(string account)
    {
        return "test";
    }
}

public class TaskStateChangeEventHandlerTests
{
    private readonly EcsTaskStateChangeEvent _testEvent = new(
        "12345", 
        "ECS Task State Change",
        "1111111111",
        DateTime.Now,
        "eu-west-2",
        new EcsEventDetail(
            DateTime.Now, 
            "1024", 
            "1024", 
            "RUNNING", 
            "RUNNING",
            [],
            "arn:aws:ecs:eu-west-2:506190012364:task-definition/cdp-example-node-backend:47",
            "task-arn",
            "reason",
            "ecs-svc/6276605373259507742",
            "ecs-svc/6276605373259507742"),
        "ecs-svc/6276605373259507742",
        "ecs-svc/6276605373259507742"
    );

    
    [Fact]
    public async Task TestUpdatesUsingLinkedRecord()
    {

        var config = new OptionsWrapper<EcsEventListenerOptions>(new EcsEventListenerOptions());
        
        var deployableArtifactsService = Substitute.For<IDeployableArtifactsService>();
        var deploymentsService = Substitute.For<IDeploymentsServiceV2>();
        var testRunService = Substitute.For<ITestRunService>();

        deploymentsService.FindDeploymentByLambdaId("ecs-svc/6276605373259507742", Arg.Any<CancellationToken>())
            .Returns(new DeploymentV2());
        
        var handler = new TaskStateChangeEventHandler(config, 
            new MockEnvironmentLookup(),
            deploymentsService, 
            deployableArtifactsService, 
            testRunService,
            ConsoleLogger.CreateLogger<TaskStateChangeEventHandler>());
        
        await handler.UpdateDeployment(_testEvent, new CancellationToken());
        
        await deploymentsService.Received().FindDeploymentByLambdaId("ecs-svc/6276605373259507742", Arg.Any<CancellationToken>());
        await deploymentsService.DidNotReceiveWithAnyArgs().FindDeploymentByTaskArn(Arg.Any<string>(), Arg.Any<CancellationToken>());
        await deploymentsService.Received().UpdateDeployment(Arg.Any<DeploymentV2>(), Arg.Any<CancellationToken>());
    }
    
    [Fact]
    public async Task TestFallbackLinking()
    {

        var config = new OptionsWrapper<EcsEventListenerOptions>(new EcsEventListenerOptions());
        
        var deployableArtifactsService = Substitute.For<IDeployableArtifactsService>();
        var deploymentsService = Substitute.For<IDeploymentsServiceV2>();
        var testRunService = Substitute.For<ITestRunService>();

        deploymentsService.FindDeploymentByLambdaId("ecs-svc/6276605373259507742", Arg.Any<CancellationToken>()).ReturnsNull();
        deploymentsService.FindDeploymentByTaskArn("arn:aws:ecs:eu-west-2:506190012364:task-definition/cdp-example-node-backend:47", Arg.Any<CancellationToken>()).Returns(new DeploymentV2());
        
        
        var handler = new TaskStateChangeEventHandler(config, 
            new MockEnvironmentLookup(),
            deploymentsService, 
            deployableArtifactsService, 
            testRunService,
            ConsoleLogger.CreateLogger<TaskStateChangeEventHandler>());
        
        await handler.UpdateDeployment(_testEvent, new CancellationToken());
        
        await deploymentsService.Received().FindDeploymentByLambdaId("ecs-svc/6276605373259507742", Arg.Any<CancellationToken>());
        await deploymentsService.Received().FindDeploymentByTaskArn("arn:aws:ecs:eu-west-2:506190012364:task-definition/cdp-example-node-backend:47", Arg.Any<CancellationToken>());
        await deploymentsService.Received().UpdateDeployment(Arg.Any<DeploymentV2>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestNotUpdatedWhenNoLinkExists()
    {

        var config = new OptionsWrapper<EcsEventListenerOptions>(new EcsEventListenerOptions());
        var deployableArtifactsService = Substitute.For<IDeployableArtifactsService>();
        var deploymentsService = Substitute.For<IDeploymentsServiceV2>();
        var testRunService = Substitute.For<ITestRunService>();

        deploymentsService.FindDeploymentByLambdaId("ecs-svc/6276605373259507742", Arg.Any<CancellationToken>()).ReturnsNull();
        deploymentsService
            .FindDeploymentByTaskArn("arn:aws:ecs:eu-west-2:506190012364:task-definition/cdp-example-node-backend:47",
                Arg.Any<CancellationToken>()).ReturnsNull();
        
        
        var handler = new TaskStateChangeEventHandler(config, 
            new MockEnvironmentLookup(),
            deploymentsService, 
            deployableArtifactsService, 
            testRunService,
            ConsoleLogger.CreateLogger<TaskStateChangeEventHandler>());

        await handler.UpdateDeployment(_testEvent, new CancellationToken());
        
        await deploymentsService.Received().FindDeploymentByLambdaId("ecs-svc/6276605373259507742", Arg.Any<CancellationToken>());
        await deploymentsService.Received().FindDeploymentByTaskArn("arn:aws:ecs:eu-west-2:506190012364:task-definition/cdp-example-node-backend:47", Arg.Any<CancellationToken>());
        await deploymentsService.DidNotReceive().UpdateDeployment(Arg.Any<DeploymentV2>(), Arg.Any<CancellationToken>());
    }

}