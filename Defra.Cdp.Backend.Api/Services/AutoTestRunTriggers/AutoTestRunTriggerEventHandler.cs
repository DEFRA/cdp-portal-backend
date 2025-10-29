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
    SelfServiceOpsClient selfServiceOpsClient,
    ILogger<AutoTestRunTriggerEventHandler> logger)
{
    public async Task Handle(string id, EcsDeploymentStateChangeEvent ecsEvent, CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "{Id} Handling EcsDeploymentStateChange trigger test runs for {DeploymentId}, {Name} {Reason}",
            id, ecsEvent.Detail.DeploymentId, ecsEvent.Detail.EventName, ecsEvent.Detail.Reason);

        var deployment =
            await deploymentsService.FindDeploymentByLambdaId(ecsEvent.Detail.DeploymentId, cancellationToken);
        if (deployment == null)
        {
            logger.LogWarning("{Id} Deployment {DeploymentId} not found", id, ecsEvent.Detail.DeploymentId);
        }
        else if (deployment.Status is DeploymentStatus.Failed or DeploymentStatus.Undeployed)
        {
            // SERVICE_DEPLOYMENT_COMPLETED events implicitly mean the deployment is 'running'.
            // The only exception is if it's an undeploy or failed with no rollback.
            logger.LogWarning("{Id} Deployment {DeploymentId} status is {Status}, not running tests", id,
                ecsEvent.Detail.DeploymentId, deployment.Status);
        }
        else
        {
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
                    var configsToRunFor = runConfigs
                        .Where(config => config.Environments.Contains(deployment.Environment))
                        .ToList();
                    foreach (var configToRunFor in configsToRunFor)
                    {
                        logger.LogInformation(
                            "{Id} Triggering test run for {DeploymentId} {TestSuite} in {Environment} with profile {Profile}",
                            id,
                            ecsEvent.Detail.DeploymentId, testSuite, deployment.Environment, configToRunFor.Profile);

                        var testRunSettings =
                            await testRunService.FindTestRunSettings(testSuite, deployment.Environment,
                                cancellationToken);

                        var userDetails = new UserDetails
                        {
                            Id = AutoTestRunConstants.AutoTestRunId,
                            DisplayName = "Auto test runner"
                        };

                        await selfServiceOpsClient.TriggerTestSuite(testSuite, userDetails, deployment, testRunSettings,
                            configToRunFor.Profile, cancellationToken);
                    }
                }
            }
        }
    }
}