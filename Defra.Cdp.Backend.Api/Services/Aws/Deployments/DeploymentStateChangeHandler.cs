using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Notifications;

namespace Defra.Cdp.Backend.Api.Services.Aws.Deployments;

public class DeploymentStateChangeEventHandler(
    IDeploymentsService deploymentsService,
    INotificationDispatcher notificationDispatcher,
    ILogger<DeploymentStateChangeEventHandler> logger)
{
    public async Task Handle(string id, EcsDeploymentStateChangeEvent ecsEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("{id} Handling EcsDeploymentStateChange Update {deploymentId}, {name} {reason}", id, ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason);
        var statusChange = await deploymentsService.UpdateDeploymentStatus(ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason, cancellationToken);
        await TriggerDeploymentNotification(statusChange, cancellationToken);
    }

    private async Task TriggerDeploymentNotification(ServiceStatusChange? statusChange, CancellationToken cancellationToken)
    {
        // Ensure we only trigger the alert on the status change, no subsequent failure updates
        if (statusChange is { NewStatus: DeploymentStatus.SERVICE_DEPLOYMENT_FAILED } && statusChange.OldStatus != statusChange.NewStatus)
        {
            var failureEvent = new DeploymentFailedEvent
            {
                DeploymentId = statusChange.DeploymentId,
                Entity = statusChange.EntityId,
                Environment = statusChange.Environment,
                Version = statusChange.Version
            };
            await notificationDispatcher.Dispatch(failureEvent, cancellationToken);
        }
    }
}