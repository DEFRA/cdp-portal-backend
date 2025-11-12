using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;

namespace Defra.Cdp.Backend.Api.Services.Aws.Deployments;

public class DeploymentStateChangeEventHandler(
    IDeploymentsService deploymentsService,
    ILogger<DeploymentStateChangeEventHandler> logger)
{
    public async Task Handle(string id, EcsDeploymentStateChangeEvent ecsEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation("{id} Handling EcsDeploymentStateChange Update {deploymentId}, {name} {reason}", id, ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason);
        var updated = await deploymentsService.UpdateDeploymentStatus(ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason, cancellationToken);
        if (!updated)
        {
            logger.LogWarning("{id} Failed to record EcsDeploymentStateChange {deploymentId}", id, ecsEvent.Detail.DeploymentId);
        }
    }
}