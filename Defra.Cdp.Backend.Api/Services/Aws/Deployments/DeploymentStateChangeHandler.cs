using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.DeploymentTriggers;
using Defra.Cdp.Backend.Api.Services.TestSuites;

namespace Defra.Cdp.Backend.Api.Services.Aws.Deployments;

public class DeploymentStateChangeEventHandler
{
   private readonly IDeploymentsService _deploymentsService;
   private readonly ILogger<DeploymentStateChangeEventHandler> _logger;

   public DeploymentStateChangeEventHandler(
     IDeploymentsService deploymentsService,
      ILogger<DeploymentStateChangeEventHandler> logger
   )
    {
      _deploymentsService = deploymentsService;
      _logger = logger;
   }

    public async Task Handle(string id, EcsDeploymentStateChangeEvent ecsEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{id} Handling EcsDeploymentStateChange Update {deploymentId}, {name} {reason}", id, ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason);
        var updated = await _deploymentsService.UpdateDeploymentStatus(ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason, cancellationToken);
        if (!updated)
        {
         _logger.LogWarning("{id} Failed to record EcsDeploymentStateChange {deploymentId}", id, ecsEvent.Detail.DeploymentId);
      }
   }
}
