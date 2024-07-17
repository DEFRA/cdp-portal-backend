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
    public async Task Handle(string id, EcsTaskStateChangeEvent ecsTaskStateChangeEvent, CancellationToken cancellationToken)
    {
        
        _logger.LogInformation("Processing lambda deployment message {Id}", id);
        // ID from cdp-self-service-ops
        var cdpDeploymentId = ecsTaskStateChangeEvent.CdpDeploymentId;
        
        // ID of ECS deployer
        var lambdaId = ecsTaskStateChangeEvent.Detail.EcsSvcDeploymentId ?.Trim();

        if (string.IsNullOrWhiteSpace(cdpDeploymentId) || string.IsNullOrWhiteSpace(lambdaId))
        {
            _logger.LogInformation( $"Received lambda event with missing ID(s)! cdp:[cdpDeploymentId] lambda[{lambdaId}]");
            return;
        }

        var linked = await _deploymentsServiceV2.LinkDeployment(cdpDeploymentId, lambdaId, cancellationToken);

        if (!linked)
        {
            _logger.LogWarning(
                $"Failed to link cdp ${cdpDeploymentId} to ecs ${lambdaId}. If the deployment was triggered in a different environment this is to be expected.");
            return;
        }

        _logger.LogInformation($"Successfully linked requested deployed {cdpDeploymentId} to {lambdaId}");
    }

}