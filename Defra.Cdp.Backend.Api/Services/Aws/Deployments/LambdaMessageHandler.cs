using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;

namespace Defra.Cdp.Backend.Api.Services.Aws.Deployments;

public class LambdaMessageHandler
{
    private readonly IDeploymentsService _deploymentsService;
    private readonly ILogger<LambdaMessageHandler> _logger;

    public LambdaMessageHandler(IDeploymentsService deploymentsService, ILogger<LambdaMessageHandler> logger)
    {

        _deploymentsService = deploymentsService;
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
        var lambdaId = ecsDeploymentLambdaEvent.Detail.EcsDeploymentId?.Trim();

        if (string.IsNullOrWhiteSpace(cdpDeploymentId) || string.IsNullOrWhiteSpace(lambdaId))
        {
            _logger.LogInformation("Received lambda event with missing ID ecs deployment id {lambdaId}]", lambdaId);
            return;
        }

        // Link CDP id to ECS id if needed
        var alreadyLinked = await _deploymentsService.FindDeploymentByLambdaId(lambdaId, cancellationToken) != null;
        if (!alreadyLinked)
        {
            var linked = await _deploymentsService.LinkDeployment(cdpDeploymentId, lambdaId, cancellationToken);
            if (!linked)
            {
                // If linking fails it's likely the deployment came from a different instance of portal
                // Use the original request, if present to generate the missing deployment record.
                var deployment = Deployment.FromLambdaMessage(ecsDeploymentLambdaEvent);
                if (deployment != null)
                {
                    // cdp & ecs id's are already present so no need to re-link
                    _logger.LogInformation("Creating deployment record for {cdpDeploymentId} linked to {lamdaId}. This deployment was not found in the database, it likely originated from a different portal.", cdpDeploymentId, lambdaId);
                    await _deploymentsService.RegisterDeployment(deployment, cancellationToken);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to link cdp {cdpDeploymentId} to ecs {lambdaId}. If the deployment was triggered in a different environment this is to be expected.",
                        cdpDeploymentId, lambdaId);
                    return;
                }
            }
        }

        // Update the status using the data from the lambda
        var eventName = ecsDeploymentLambdaEvent.Detail.EventName;
        var reason = ecsDeploymentLambdaEvent.Detail.Reason;
        if (eventName != null && reason != null)
        {
            await _deploymentsService.UpdateDeploymentStatus(lambdaId, eventName, reason, cancellationToken);
        }

        _logger.LogInformation("Successfully linked requested deployed {cdpDeploymentId} to {lambdaId}", cdpDeploymentId, lambdaId);
    }

}