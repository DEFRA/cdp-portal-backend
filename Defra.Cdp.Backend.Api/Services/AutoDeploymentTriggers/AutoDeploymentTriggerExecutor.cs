using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.FeatureToggles;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Utils.Auditing;
using Defra.Cdp.Backend.Api.Utils.Clients;

namespace Defra.Cdp.Backend.Api.Services.AutoDeploymentTriggers;

internal static class AutoDeploymentConstants
{
    public const string AutoDeploymentId = "00000000-0000-0000-0000-000000000000";
}

public interface IAutoDeploymentTriggerExecutor
{
    public Task Handle(string repositoryName, string imageTag, CancellationToken cancellationToken);
}

public class AutoDeploymentTriggerExecutor(
    IAutoDeploymentTriggerService autoDeploymentTriggerService,
    IDeploymentsService deploymentsService,
    SelfServiceOpsClient selfServiceOpsClient,
    IAppConfigVersionsService appConfigVersionsService,
    IFeatureTogglesService featureTogglesService,
    ILogger<AutoDeploymentTriggerExecutor> logger
) : IAutoDeploymentTriggerExecutor
{
    public async Task Handle(string repositoryName, string imageTag, CancellationToken cancellationToken)
    {
        var autoDeploymentToggle = await featureTogglesService.GetToggle("auto-deployments", cancellationToken);
        if (autoDeploymentToggle is { Active: true })
        {
            logger.LogInformation("Auto-deployment feature is disabled, skipping auto-deployment for {RepositoryName}",
                repositoryName);
            return;
        }
        
        var trigger = await autoDeploymentTriggerService.FindForService(repositoryName, cancellationToken);

        if (trigger == null)
        {
            logger.LogInformation("No auto-deployment trigger found for {RepositoryName}", repositoryName);
            return;
        }

        logger.LogInformation("Auto-deployment trigger found for {RepositoryName} to {Environments}",
            repositoryName, trigger.Environments);

        foreach (var environment in trigger.Environments)
        {
            var deploymentSettings =
                await deploymentsService.FindDeploymentSettings(repositoryName, environment, cancellationToken);
            if (deploymentSettings == null)
            {
                logger.LogError(
                    "Could not find deployment settings for repository {RepositoryName} in environment {Environment}",
                    repositoryName, environment);
                continue;
            }

            var configVersion =
                await appConfigVersionsService.FindLatestAppConfigVersion(environment, cancellationToken);
            if (configVersion == null)
            {
                logger.LogError("Could not find latest config version for environment: {Environment}", environment);
                continue;
            }

            logger.LogInformation(
                "Auto-deploying {RepositoryName} version {ImageTag} to {Environment}",
                repositoryName, imageTag, environment);

            var userDetails = new UserDetails
            {
                Id = AutoDeploymentConstants.AutoDeploymentId, DisplayName = "Auto deployment"
            };

            await selfServiceOpsClient.AutoDeployService(repositoryName, imageTag, environment, userDetails,
                deploymentSettings, configVersion.CommitSha, cancellationToken);

            logger.Audit("Auto-deploying {Repo}:{Version} to {Environment}", repositoryName, imageTag, environment);
        }
    }
}