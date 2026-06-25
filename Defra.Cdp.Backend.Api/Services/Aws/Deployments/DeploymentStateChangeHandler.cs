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
        logger.LogInformation("{Id} Handling EcsDeploymentStateChange Update {DeploymentId}, {Name} {Reason}", id, ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason);
        var statusChange = await deploymentsService.UpdateDeploymentStatus(ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason, cancellationToken);
        await TriggerDeploymentNotification(statusChange, cancellationToken);
    }

    private async Task TriggerDeploymentNotification(ServiceStatusChange? statusChange, CancellationToken cancellationToken)
    {
        // Only trigger on an actual status transition, not repeated updates with the same status
        INotificationEvent? notificationEvent = statusChange switch
        {
            { NewStatus: DeploymentStatus.SERVICE_DEPLOYMENT_FAILED, OldStatus: var old }
                when old != DeploymentStatus.SERVICE_DEPLOYMENT_FAILED
                => new DeploymentFailedEvent
                {
                    DeploymentId = statusChange.DeploymentId,
                    Entity = statusChange.EntityId,
                    Environment = statusChange.Environment,
                    Version = statusChange.Version,
                    UserDisplayName = statusChange.UserDisplayName
                },
            { NewStatus: DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED, OldStatus: var old }
                when old != DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED
                => new DeploymentSuccessEvent
                {
                    DeploymentId = statusChange.DeploymentId,
                    Entity = statusChange.EntityId,
                    Environment = statusChange.Environment,
                    Version = statusChange.Version,
                    UserDisplayName = statusChange.UserDisplayName
                },
            _ => null
        };

        if (notificationEvent != null)
            await notificationDispatcher.Dispatch(notificationEvent, cancellationToken);
    }
}