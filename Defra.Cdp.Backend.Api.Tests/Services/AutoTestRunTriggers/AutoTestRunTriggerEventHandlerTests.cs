using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Defra.Cdp.Backend.Api.Utils.Clients;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.AutoTestRunTriggers;

public class AutoTestRunTriggerEventHandlerTests
{
    private IDeploymentsService _deploymentsService = Substitute.For<IDeploymentsService>();
    private IAutoTestRunTriggerService _autoTestRunTriggerService = Substitute.For<IAutoTestRunTriggerService>();
    private ITestRunService _testRunService = Substitute.For<ITestRunService>();
    private ISelfServiceOpsClient _selfServiceOpsClient = Substitute.For<ISelfServiceOpsClient>();

    private readonly EcsDeploymentStateChangeEvent _deploymentEvent = new(
        "ECS Deployment State Change",
        "0000000000",
        new EcsDeploymentStateChangeDetail("INFO", DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED, "ecs/1234",
            new DateTime(), "")
    );

    private readonly Deployment _deployment = new() { Environment = "dev", Status = DeploymentStatus.Running };

    [Fact]
    public async Task Test_it_triggers_testsuite_in_same_env_as_deployment()
    {
        var handler = new AutoTestRunTriggerEventHandler(_deploymentsService, _autoTestRunTriggerService,
            _testRunService,
            _selfServiceOpsClient, new NullLogger<AutoTestRunTriggerEventHandler>());


        _deploymentsService
            .FindDeploymentByLambdaId(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_deployment);

        _autoTestRunTriggerService.FindForService(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
            new AutoTestRunTrigger
            {
                ServiceName = "foo",
                TestSuites = new Dictionary<string, List<TestSuiteRunConfig>>
                {
                    { "foo-tests", [new TestSuiteRunConfig { Environments = ["dev", "test"] }] }
                }
            });

        _testRunService
            .AnyTestRunExists(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await handler.Handle("", _deploymentEvent, TestContext.Current.CancellationToken);

        await _selfServiceOpsClient.Received(1).TriggerTestSuite(
            Arg.Is("foo-tests"),
            Arg.Any<UserDetails>(),
            Arg.Is(_deployment),
            Arg.Any<TestRunSettings>(),
            Arg.Any<string>(),
            Arg.Any<CancellationToken>()
        );
    }

    [Fact]
    public async Task Test_it_doesnt_triggers_testsuite_if_not_configured_for_env()
    {
        var handler = new AutoTestRunTriggerEventHandler(_deploymentsService, _autoTestRunTriggerService,
            _testRunService,
            _selfServiceOpsClient, new NullLogger<AutoTestRunTriggerEventHandler>());

        _deploymentsService
            .FindDeploymentByLambdaId(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_deployment);

        _autoTestRunTriggerService.FindForService(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
            new AutoTestRunTrigger
            {
                ServiceName = "foo",
                TestSuites = new Dictionary<string, List<TestSuiteRunConfig>>
                {
                    { "foo-tests", [new TestSuiteRunConfig { Environments = ["test"] }] }
                }
            });

        _testRunService
            .AnyTestRunExists(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await handler.Handle("", _deploymentEvent, TestContext.Current.CancellationToken);

        await _selfServiceOpsClient.DidNotReceiveWithAnyArgs()
            .TriggerTestSuite(
                Arg.Any<string>(),
                Arg.Any<UserDetails>(),
                Arg.Any<Deployment>(),
                Arg.Any<TestRunSettings>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }
    
    [Fact]
    public async Task Test_it_doesnt_triggers_testsuite_for_non_complete_messages()
    {
        var handler = new AutoTestRunTriggerEventHandler(_deploymentsService, _autoTestRunTriggerService,
            _testRunService,
            _selfServiceOpsClient, new NullLogger<AutoTestRunTriggerEventHandler>());

        var inProgressDeploymentEvent = new EcsDeploymentStateChangeEvent(
            "ECS Deployment State Change",
            "0000000000",
            new EcsDeploymentStateChangeDetail("INFO", DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED, "ecs/1234",
                new DateTime(), "")
        );

        await handler.Handle("", inProgressDeploymentEvent, TestContext.Current.CancellationToken);

        await _selfServiceOpsClient.DidNotReceiveWithAnyArgs()
            .TriggerTestSuite(
                Arg.Any<string>(),
                Arg.Any<UserDetails>(),
                Arg.Any<Deployment>(),
                Arg.Any<TestRunSettings>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }
    
    [Fact]
    public async Task Test_it_doesnt_triggers_testsuite_if_service_was_undeployed() 
    {
        var handler = new AutoTestRunTriggerEventHandler(_deploymentsService, _autoTestRunTriggerService,
            _testRunService,
            _selfServiceOpsClient, new NullLogger<AutoTestRunTriggerEventHandler>());

        
        var undeployment = new Deployment() { Environment = "dev", Status = DeploymentStatus.Undeployed };
        _deploymentsService
            .FindDeploymentByLambdaId(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(undeployment);

        _autoTestRunTriggerService.FindForService(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(
            new AutoTestRunTrigger
            {
                ServiceName = "foo",
                TestSuites = new Dictionary<string, List<TestSuiteRunConfig>>
                {
                    { "foo-tests", [new TestSuiteRunConfig { Environments = ["dev"] }] }
                }
            });

        _testRunService
            .AnyTestRunExists(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(false);

        await handler.Handle("", _deploymentEvent, TestContext.Current.CancellationToken);

        await _selfServiceOpsClient.DidNotReceiveWithAnyArgs()
            .TriggerTestSuite(
                Arg.Any<string>(),
                Arg.Any<UserDetails>(),
                Arg.Any<Deployment>(),
                Arg.Any<TestRunSettings>(),
                Arg.Any<string?>(),
                Arg.Any<CancellationToken>());
    }
}