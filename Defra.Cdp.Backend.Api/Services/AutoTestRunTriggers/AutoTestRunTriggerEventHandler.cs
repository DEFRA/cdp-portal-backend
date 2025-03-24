using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Utils.Clients;

namespace Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;

public class AutoTestRunTriggerEventHandler(
    IDeploymentsService deploymentsService,
    IAutoTestRunTriggerService autoTestRunTriggerService,
    SelfServiceOpsClient selfServiceOpsClient,
    ILogger<AutoTestRunTriggerEventHandler> logger)
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
            var trigger = await autoTestRunTriggerService.FindForService(deployment.Service, cancellationToken);

            var testSuitesOrEmptyList =
                trigger?.EnvironmentTestSuitesMap.GetValueOrDefault(deployment.Environment, []) ?? [];

            foreach (var testSuite in testSuitesOrEmptyList)
            {
                logger.LogInformation("{id} Triggering test run for {deploymentId} {testSuite} in {environment}",
                    id,
                    ecsEvent.Detail.DeploymentId, testSuite, deployment.Environment);

                await selfServiceOpsClient.TriggerTestSuite(testSuite, deployment.Environment, deployment.User,
                    cancellationToken);
            }
        }
    }
}