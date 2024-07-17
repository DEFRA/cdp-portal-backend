using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;

namespace Defra.Cdp.Backend.Api.Services.Aws.Deployments;

public class DeploymentStateChangeEventHandler
{
    private readonly IDeploymentsServiceV2 _deploymentsService;
    private readonly ILogger<DeploymentStateChangeEventHandler> _logger;

    public DeploymentStateChangeEventHandler(IDeploymentsServiceV2 deploymentsService, ILogger<DeploymentStateChangeEventHandler> logger)
    {
        _deploymentsService = deploymentsService;
        _logger = logger;
    }

    public async Task Handle(string id, EcsDeploymentStateChange ecsEvent, CancellationToken cancellationToken)
    {
        _logger.LogInformation("{id} Handling EcsDeploymentStateChange Update {deploymentId}, {name} {reason}", id, ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason);
        var updated = await _deploymentsService.UpdateDeploymentStatus(ecsEvent.Detail.DeploymentId, ecsEvent.Detail, cancellationToken);
        _logger.LogInformation("{id} EcsDeploymentStateChange Update {deploymentId}, {result}",  id, ecsEvent.Detail.DeploymentId, updated ? "completed" : "failed");
    }
}