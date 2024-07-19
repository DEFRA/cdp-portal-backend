using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;

namespace Defra.Cdp.Backend.Api.Services.Aws.Deployments;

public class LambdaMessageHandlerV2
{
    private readonly IDeploymentsServiceV2 _deploymentsServiceV2;
    private readonly ILogger<LambdaMessageHandlerV2> _logger;

    public LambdaMessageHandlerV2(IDeploymentsServiceV2 deploymentsServiceV2, ILogger<LambdaMessageHandlerV2> logger)
    {

        _deploymentsServiceV2 = deploymentsServiceV2;
        _logger = logger;
    }
        
    // We need to link the requested deployment (before the deployment has started) to the actual deployment as
    // actioned by the lambda. 
    public async Task Handle(string id, EcsDeploymentLambdaEvent ecsDeploymentLambdaEvent, CancellationToken cancellationToken)
    {
        
        _logger.LogInformation("Processing lambda deployment message {Id}", id);
        // ID from cdp-self-service-ops
        var cdpDeploymentId = ecsDeploymentLambdaEvent.CdpDeploymentId;
        
        // ID of ECS deployer
        var lambdaId = ecsDeploymentLambdaEvent.Detail.EcsDeploymentId ?.Trim();

        if (string.IsNullOrWhiteSpace(cdpDeploymentId) || string.IsNullOrWhiteSpace(lambdaId))
        {
            _logger.LogInformation("Received lambda event with missing ID ecs deployment id {lambdaId}]", lambdaId);
            return;
        }

        // Link CDP id to ECS id if needed
        var alreadyLinked = await _deploymentsServiceV2.FindDeploymentByLambdaId(lambdaId, cancellationToken) != null;
        if (!alreadyLinked)
        {
            var linked = await _deploymentsServiceV2.LinkDeployment(cdpDeploymentId, lambdaId, cancellationToken);
            if (!linked)
            {
                _logger.LogWarning(
                    "Failed to link cdp ${cdpDeploymentId} to ecs ${lambdaId}. If the deployment was triggered in a different environment this is to be expected.",
                    cdpDeploymentId, lambdaId);
                return;
            }
        }
        
        // Update the status using the data from the lambda
        var eventName = ecsDeploymentLambdaEvent.Detail.EventName;
        var reason = ecsDeploymentLambdaEvent.Detail.Reason;
        if (eventName != null && reason != null)
        {
            await _deploymentsServiceV2.UpdateDeploymentStatus(lambdaId, eventName, reason, cancellationToken);
        }

        _logger.LogInformation($"Successfully linked requested deployed {cdpDeploymentId} to {lambdaId}");
    }

}