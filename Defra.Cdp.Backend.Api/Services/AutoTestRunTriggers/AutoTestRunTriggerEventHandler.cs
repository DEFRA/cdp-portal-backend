using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Defra.Cdp.Backend.Api.Utils.Clients;

namespace Defra.Cdp.Backend.Api.Services.AutoTestRunTriggers;

internal static class AutoTestRunConstants
{
    public const string AutoTestRunId = "11111111-1111-1111-1111-111111111111";
}

public class AutoTestRunTriggerEventHandler(
    IDeploymentsService deploymentsService,
    IAutoTestRunTriggerService autoTestRunTriggerService,
    ITestRunService testRunService,
    ISelfServiceOpsClient selfServiceOpsClient,
    ILogger<AutoTestRunTriggerEventHandler> logger)
{
    public async Task Handle(string id, EcsDeploymentStateChangeEvent ecsEvent, CancellationToken cancellationToken)
    {
        if (ecsEvent.Detail.EventName != DeploymentStatus.SERVICE_DEPLOYMENT_COMPLETED)
        {
            logger.LogDebug("ignoring non service_deployment_complete event");
            return;
        }

        logger.LogInformation(
            "{Id} Handling EcsDeploymentStateChange trigger test runs for {DeploymentId}, {Name} {Reason}",
            id, ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason);


        var deployment =
            await deploymentsService.FindDeploymentByLambdaId(ecsEvent.Detail.DeploymentId, cancellationToken);
        if (deployment == null)
        {
            logger.LogWarning("{Id} Deployment {DeploymentId} not found", id, ecsEvent.Detail.DeploymentId);
            return;
        }

        if (deployment.Status is DeploymentStatus.Failed or DeploymentStatus.Undeployed)
        {
            // SERVICE_DEPLOYMENT_COMPLETED events implicitly mean the deployment is 'running'.
            // The only exception is if it's an undeploy or failed with no rollback.
            logger.LogWarning("{Id} Deployment {DeploymentId} status is {Status}, not running tests", id,
                ecsEvent.Detail.DeploymentId, deployment.Status);
            return;
        }

        var trigger = await autoTestRunTriggerService.FindForService(deployment.Service, cancellationToken);

        foreach (var (testSuite, runConfigs) in trigger?.TestSuites ?? [])
        {
            var anyTestRunsExist = await testRunService.AnyTestRunExists(testSuite, deployment.Environment,
                ecsEvent.Detail.DeploymentId, cancellationToken);

            if (anyTestRunsExist)
            {
                logger.LogInformation(
                    "{Id} Not triggering test run for {DeploymentId} {TestSuite} in {Environment} as test run(s) exist",
                    id, ecsEvent.Detail.DeploymentId, testSuite, deployment.Environment);
            }
            else
            {
                var autoTestConfigs = runConfigs
                    .Where(config => config.Environments.Contains(deployment.Environment))
                    .ToList();
                foreach (var autoTestConfig in autoTestConfigs)
                {
                    logger.LogInformation(
                        "{Id} Triggering test run for {DeploymentId} {TestSuite} in {Environment} with profile {Profile}",
                        id,
                        ecsEvent.Detail.DeploymentId, testSuite, deployment.Environment, autoTestConfig.Profile);

                    var testRunSettings =
                        await testRunService.FindTestRunSettings(testSuite, deployment.Environment,
                            cancellationToken);

                    var userDetails = new UserDetails
                    {
                        Id = AutoTestRunConstants.AutoTestRunId, DisplayName = "Auto test runner"
                    };

                    logger.LogInformation(
                        "Triggering test suite {Name} in {Environment} from from deployment {DeployEnv}/{DeploymentId}",
                        testSuite, deployment.Environment, deployment.Environment, deployment.CdpDeploymentId);

                    await selfServiceOpsClient.TriggerTestSuite(testSuite, userDetails, deployment, testRunSettings,
                        autoTestConfig.Profile, cancellationToken);
                }
            }
        }
    }
}