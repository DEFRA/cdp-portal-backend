using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Notifications;
using Defra.Cdp.Backend.Api.Services.Snow;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ReturnsExtensions;

namespace Defra.Cdp.Backend.Api.Tests.Services.Aws.Deployments;

public class DeploymentStateChangeEventHandlerTests
{
    private readonly EcsDeploymentStateChangeEvent _deploymentFailedEvent = new(
        "ECS Deployment State Change",
        "111111111111",
        new EcsDeploymentStateChangeDetail(
            "ERROR",
            DeploymentStatus.SERVICE_DEPLOYMENT_FAILED,
            "ecs-svc/123",
            DateTime.UtcNow,
            "service deployment failed"
        )
    );

    [Fact]
    public async Task DispatchesFailureNotificationWhenDeploymentFails()
    {
        var deploymentsService = Substitute.For<IDeploymentsService>();
        var notificationDispatcher = Substitute.For<INotificationDispatcher>();
        var snowDeploymentTriggerService = Substitute.For<ISnowDeploymentTriggerService>();
        var statusChange = StatusChange(
            oldStatus: DeploymentStatus.SERVICE_DEPLOYMENT_IN_PROGRESS,
            newStatus: DeploymentStatus.SERVICE_DEPLOYMENT_FAILED);

        deploymentsService
            .UpdateDeploymentStatus("ecs-svc/123", DeploymentStatus.SERVICE_DEPLOYMENT_FAILED, "service deployment failed", Arg.Any<CancellationToken>())
            .Returns(statusChange);

        var handler = Handler(deploymentsService, notificationDispatcher, snowDeploymentTriggerService);

        await handler.Handle("message-1", _deploymentFailedEvent, TestContext.Current.CancellationToken);

        await deploymentsService.Received().UpdateDeploymentStatus(
            "ecs-svc/123",
            DeploymentStatus.SERVICE_DEPLOYMENT_FAILED,
            "service deployment failed",
            Arg.Any<CancellationToken>());
        await notificationDispatcher.Received().Dispatch(
            Arg.Is<DeploymentFailedEvent>(e =>
                e.DeploymentId == statusChange.DeploymentId &&
                e.Entity == statusChange.EntityId &&
                e.Environment == statusChange.Environment &&
                e.Version == statusChange.Version &&
                e.UserDisplayName == statusChange.UserDisplayName),
            Arg.Any<CancellationToken>());
        await snowDeploymentTriggerService.DidNotReceive().TriggerDeploymentRecord(
            Arg.Any<ServiceStatusChange>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DispatchesSuccessNotificationWhenDeploymentCompletes()
    {
        var deploymentsService = Substitute.For<IDeploymentsService>();
        var notificationDispatcher = Substitute.For<INotificationDispatcher>();
        var snowDeploymentTriggerService = Substitute.For<ISnowDeploymentTriggerService>();
        var statusChange = StatusChange(
            oldStatus: DeploymentStatus.SERVICE_DEPLOYMENT_IN_PROGRESS,
            newStatus: DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED);
        var completedEvent = _deploymentFailedEvent with
        {
            Detail = _deploymentFailedEvent.Detail with
            {
                EventType = "INFO",
                EventName = DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED,
                Reason = "service deployment completed"
            }
        };

        deploymentsService
            .UpdateDeploymentStatus("ecs-svc/123", DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED, "service deployment completed", Arg.Any<CancellationToken>())
            .Returns(statusChange);

        var handler = Handler(deploymentsService, notificationDispatcher, snowDeploymentTriggerService);

        await handler.Handle("message-1", completedEvent, TestContext.Current.CancellationToken);

        await notificationDispatcher.Received().Dispatch(
            Arg.Is<DeploymentSuccessEvent>(e =>
                e.DeploymentId == statusChange.DeploymentId &&
                e.Entity == statusChange.EntityId &&
                e.Environment == statusChange.Environment &&
                e.Version == statusChange.Version &&
                e.PreviousVersion == statusChange.PreviousVersion &&
                e.UserDisplayName == statusChange.UserDisplayName),
            Arg.Any<CancellationToken>());
        await snowDeploymentTriggerService.DidNotReceive().TriggerDeploymentRecord(
            Arg.Any<ServiceStatusChange>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotDispatchNotificationWhenDeploymentStatusHasNotChanged()
    {
        var deploymentsService = Substitute.For<IDeploymentsService>();
        var notificationDispatcher = Substitute.For<INotificationDispatcher>();
        var snowDeploymentTriggerService = Substitute.For<ISnowDeploymentTriggerService>();

        deploymentsService
            .UpdateDeploymentStatus("ecs-svc/123", DeploymentStatus.SERVICE_DEPLOYMENT_FAILED, "service deployment failed", Arg.Any<CancellationToken>())
            .Returns(StatusChange(
                oldStatus: DeploymentStatus.SERVICE_DEPLOYMENT_FAILED,
                newStatus: DeploymentStatus.SERVICE_DEPLOYMENT_FAILED));

        var handler = Handler(deploymentsService, notificationDispatcher, snowDeploymentTriggerService);

        await handler.Handle("message-1", _deploymentFailedEvent, TestContext.Current.CancellationToken);

        await notificationDispatcher.DidNotReceive().Dispatch(
            Arg.Any<INotificationEvent>(),
            Arg.Any<CancellationToken>());
        await snowDeploymentTriggerService.DidNotReceive().TriggerDeploymentRecord(
            Arg.Any<ServiceStatusChange>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotDispatchNotificationWhenDeploymentStatusIsNotFound()
    {
        var deploymentsService = Substitute.For<IDeploymentsService>();
        var notificationDispatcher = Substitute.For<INotificationDispatcher>();
        var snowDeploymentTriggerService = Substitute.For<ISnowDeploymentTriggerService>();

        deploymentsService
            .UpdateDeploymentStatus("ecs-svc/123", DeploymentStatus.SERVICE_DEPLOYMENT_FAILED, "service deployment failed", Arg.Any<CancellationToken>())
            .ReturnsNull();

        var handler = Handler(deploymentsService, notificationDispatcher, snowDeploymentTriggerService);

        await handler.Handle("message-1", _deploymentFailedEvent, TestContext.Current.CancellationToken);

        await notificationDispatcher.DidNotReceive().Dispatch(
            Arg.Any<INotificationEvent>(),
            Arg.Any<CancellationToken>());
        await snowDeploymentTriggerService.DidNotReceive().TriggerDeploymentRecord(
            Arg.Any<ServiceStatusChange>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggersSnowWorkflowWhenProductionDeploymentCompletes()
    {
        var deploymentsService = Substitute.For<IDeploymentsService>();
        var notificationDispatcher = Substitute.For<INotificationDispatcher>();
        var snowDeploymentTriggerService = Substitute.For<ISnowDeploymentTriggerService>();
        var statusChange = StatusChange(
            oldStatus: DeploymentStatus.SERVICE_DEPLOYMENT_IN_PROGRESS,
            newStatus: DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED,
            environment: "prod");
        var completedEvent = _deploymentFailedEvent with
        {
            Detail = _deploymentFailedEvent.Detail with
            {
                EventType = "INFO",
                EventName = DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED,
                Reason = "service deployment completed"
            }
        };

        deploymentsService
            .UpdateDeploymentStatus("ecs-svc/123", DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED,
                "service deployment completed", Arg.Any<CancellationToken>())
            .Returns(statusChange);

        var handler = Handler(deploymentsService, notificationDispatcher, snowDeploymentTriggerService);

        await handler.Handle("message-1", completedEvent, TestContext.Current.CancellationToken);

        await snowDeploymentTriggerService.Received(1).TriggerDeploymentRecord(
            Arg.Is<ServiceStatusChange>(change => change.Environment == "prod"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TriggersSnowWorkflowWhenAdditionalEnvironmentIsConfigured()
    {
        var deploymentsService = Substitute.For<IDeploymentsService>();
        var notificationDispatcher = Substitute.For<INotificationDispatcher>();
        var snowDeploymentTriggerService = Substitute.For<ISnowDeploymentTriggerService>();
        var statusChange = StatusChange(
            oldStatus: DeploymentStatus.SERVICE_DEPLOYMENT_IN_PROGRESS,
            newStatus: DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED,
            environment: "infra-dev");
        var completedEvent = _deploymentFailedEvent with
        {
            Detail = _deploymentFailedEvent.Detail with
            {
                EventType = "INFO",
                EventName = DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED,
                Reason = "service deployment completed"
            }
        };

        deploymentsService
            .UpdateDeploymentStatus("ecs-svc/123", DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED,
                "service deployment completed", Arg.Any<CancellationToken>())
            .Returns(statusChange);

        var handler = Handler(deploymentsService, notificationDispatcher, snowDeploymentTriggerService,
            triggerEnvironments: ["prod", "infra-dev"]);

        await handler.Handle("message-1", completedEvent, TestContext.Current.CancellationToken);

        await snowDeploymentTriggerService.Received(1).TriggerDeploymentRecord(
            Arg.Is<ServiceStatusChange>(change => change.Environment == "infra-dev"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DoesNotTriggerSnowWorkflowForEnvironmentNotInConfiguredList()
    {
        var deploymentsService = Substitute.For<IDeploymentsService>();
        var notificationDispatcher = Substitute.For<INotificationDispatcher>();
        var snowDeploymentTriggerService = Substitute.For<ISnowDeploymentTriggerService>();
        var statusChange = StatusChange(
            oldStatus: DeploymentStatus.SERVICE_DEPLOYMENT_IN_PROGRESS,
            newStatus: DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED,
            environment: "infra-dev");
        var completedEvent = _deploymentFailedEvent with
        {
            Detail = _deploymentFailedEvent.Detail with
            {
                EventType = "INFO",
                EventName = DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED,
                Reason = "service deployment completed"
            }
        };

        deploymentsService
            .UpdateDeploymentStatus("ecs-svc/123", DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED,
                "service deployment completed", Arg.Any<CancellationToken>())
            .Returns(statusChange);

        var handler = Handler(deploymentsService, notificationDispatcher, snowDeploymentTriggerService,
            triggerEnvironments: ["prod"]);

        await handler.Handle("message-1", completedEvent, TestContext.Current.CancellationToken);

        await snowDeploymentTriggerService.DidNotReceive().TriggerDeploymentRecord(
            Arg.Any<ServiceStatusChange>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SwallowsSnowTriggerExceptions()
    {
        var deploymentsService = Substitute.For<IDeploymentsService>();
        var notificationDispatcher = Substitute.For<INotificationDispatcher>();
        var snowDeploymentTriggerService = Substitute.For<ISnowDeploymentTriggerService>();
        var statusChange = StatusChange(
            oldStatus: DeploymentStatus.SERVICE_DEPLOYMENT_IN_PROGRESS,
            newStatus: DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED,
            environment: "prod");
        var completedEvent = _deploymentFailedEvent with
        {
            Detail = _deploymentFailedEvent.Detail with
            {
                EventType = "INFO",
                EventName = DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED,
                Reason = "service deployment completed"
            }
        };

        deploymentsService
            .UpdateDeploymentStatus("ecs-svc/123", DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED,
                "service deployment completed", Arg.Any<CancellationToken>())
            .Returns(statusChange);
        snowDeploymentTriggerService
            .When(service => service.TriggerDeploymentRecord(Arg.Any<ServiceStatusChange>(), Arg.Any<CancellationToken>()))
            .Do(_ => throw new InvalidOperationException("Boom"));

        var handler = Handler(deploymentsService, notificationDispatcher, snowDeploymentTriggerService);

        var ex = await Record.ExceptionAsync(() =>
            handler.Handle("message-1", completedEvent, TestContext.Current.CancellationToken));

        Assert.Null(ex);
    }

    private static DeploymentStateChangeEventHandler Handler(
        IDeploymentsService deploymentsService,
        INotificationDispatcher notificationDispatcher,
        ISnowDeploymentTriggerService snowDeploymentTriggerService,
        string[]? triggerEnvironments = null)
    {
        var snowOptions = Options.Create(new SnowOptions
        {
            TriggerEnvironments = triggerEnvironments ?? ["prod"]
        });

        return new DeploymentStateChangeEventHandler(
            deploymentsService,
            notificationDispatcher,
            snowDeploymentTriggerService,
            snowOptions,
            NullLogger<DeploymentStateChangeEventHandler>.Instance);
    }

    private static ServiceStatusChange StatusChange(string? oldStatus, string newStatus, string environment = "dev")
    {
        return new ServiceStatusChange
        {
            DeploymentId = "ecs-svc/123",
            Environment = environment,
            OldStatus = oldStatus,
            NewStatus = newStatus,
            EntityId = "cdp-portal-backend",
            Version = "1.2.3",
            PreviousVersion = "1.2.2",
            UserDisplayName = "A User"
        };
    }
}
