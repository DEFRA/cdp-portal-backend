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
        else if (deployment.Status != DeploymentStatus.Running)
        {
            logger.LogWarning("{Id} Deployment {DeploymentId} not running", id, ecsEvent.Detail.DeploymentId);
        }
        else
        {
            var trigger = await autoTestRunTriggerService.FindForService(deployment.Service, cancellationToken);
            var environmentTestSuites = trigger?.TestSuites
                .Where(kvp => kvp.Value.Contains(deployment.Environment))
                .Select(kvp => kvp.Key)
                .ToList() ?? [];

            foreach (var testSuite in environmentTestSuites)
            {
                logger.LogInformation("{Id} Triggering test run for {DeploymentId} {TestSuite} in {Environment}",
                    id,
                    ecsEvent.Detail.DeploymentId, testSuite, deployment.Environment);

                var testRunSettings =
                    await testRunService.FindTestRunSettings(testSuite, deployment.Environment, cancellationToken);

                var userDetails = new UserDetails
                {
                    Id = AutoTestRunConstants.AutoTestRunId,
                    DisplayName = "Auto test runner"
                };

                await selfServiceOpsClient.TriggerTestSuite(testSuite, userDetails, deployment, testRunSettings,
                    cancellationToken);
            }
        }
    }
}