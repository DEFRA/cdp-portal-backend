using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Utils.Clients;

namespace Defra.Cdp.Backend.Api.Services.AutoDeploymentTriggers;

internal static class AutoDeploymentConstants
{
    public const String AutoDeploymentId = "00000000-0000-0000-0000-000000000000";
}

public interface IAutoDeploymentTriggerExecutor
{
    public Task Handle(String repositoryName, String imageTag, CancellationToken cancellationToken);
}

public class AutoDeploymentTriggerExecutor(
    IAutoDeploymentTriggerService autoDeploymentTriggerService,
    IDeploymentsService deploymentsService,
    SelfServiceOpsClient selfServiceOpsClient,
    IAppConfigVersionsService appConfigVersionsService,
    ILogger<AutoDeploymentTriggerExecutor> logger
) : IAutoDeploymentTriggerExecutor
{
    public async Task Handle(String repositoryName, String imageTag, CancellationToken cancellationToken)
    {
        var trigger = await autoDeploymentTriggerService.FindForService(repositoryName, cancellationToken);

        if (trigger != null)
        {
            logger.LogInformation("Auto-deployment trigger found for {repositoryName} to {Environments}",
                repositoryName, trigger.Environments);

            foreach (var environment in trigger.Environments)
            {
                var deploymentSettings =
                    await deploymentsService.FindDeploymentSettings(repositoryName, environment, cancellationToken);
                if (deploymentSettings == null)
                {
                    logger.LogError(
                        "Could not find deployment settings for repository {repositoryName} in environment {environment}",
                        repositoryName, environment);
                    continue;
                }

                var configVersion = await appConfigVersionsService.FindLatestAppConfigVersion(environment, cancellationToken);
                if (configVersion == null)
                {
                    logger.LogError("Could not find latest config version for environment: {environment}", environment);
                    continue;
                }

                logger.LogInformation(
                    "Auto-deploying {repositoryName} version {imageTag} to {environment}",
                    repositoryName, imageTag, environment);

                var userDetails = JsonSerializer.Deserialize<UserDetails>(" { \"id\": \"" +
                                                                          AutoDeploymentConstants.AutoDeploymentId
                                                                          + "\", \"displayName\": \"Auto-deployment\" } ")
                    !;

                await selfServiceOpsClient.AutoDeployService(repositoryName, imageTag, environment, userDetails,
                    deploymentSettings, configVersion.CommitSha, cancellationToken);
            }
        }
    }
}