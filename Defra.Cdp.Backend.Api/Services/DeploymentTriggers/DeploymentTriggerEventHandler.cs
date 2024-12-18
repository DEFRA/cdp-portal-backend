using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Deployments;

namespace Defra.Cdp.Backend.Api.Services.DeploymentTriggers;

public class DeploymentTriggerEventHandler(
    IDeploymentsServiceV2 deploymentsService,
    IDeploymentTriggerService deploymentTriggerService,
    SelfServiceOpsFetcher selfServiceOpsFetcher,
    ILogger<DeploymentTriggerEventHandler> logger)
{
    public async Task Handle(string id, EcsDeploymentStateChangeEvent ecsEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "{id} Handling EcsDeploymentStateChange trigger test runs for {deploymentId}, {name} {reason}",
            id, ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason);

        var deployment =
            await deploymentsService.FindDeploymentByLambdaId(ecsEvent.Detail.DeploymentId, cancellationToken);
        if (deployment == null)
        {
            logger.LogWarning("{id} Deployment {deploymentId} not found", id, ecsEvent.Detail.DeploymentId);
        }
        else if (deployment.Status != DeploymentStatus.Running)
        {
            logger.LogWarning("{id} Deployment {deploymentId} not running", id, ecsEvent.Detail.DeploymentId);
        }
        else
        {
            var deploymentTriggers =
                await deploymentTriggerService.FindTriggersForDeployment(deployment, cancellationToken);

            foreach (var trigger in deploymentTriggers)
            {
                logger.LogInformation("{id} Triggering test run for {deploymentId} {testSuite}", id,
                    ecsEvent.Detail.DeploymentId, trigger.TestSuite);

                await selfServiceOpsFetcher.TriggerTestSuite(trigger.TestSuite, deployment.Environment, deployment.User,
                    cancellationToken);
            }
        }
    }
}