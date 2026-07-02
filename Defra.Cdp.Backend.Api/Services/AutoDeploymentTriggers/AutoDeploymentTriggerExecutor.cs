using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.FeatureToggles;
using Defra.Cdp.Backend.Api.Utils.Auditing;
using Defra.Cdp.Backend.Api.Services.Deployments;

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
    IServiceDeploymentExecutor serviceDeploymentExecutor,
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

        var userDetails = new UserDetails
        {
            Id = AutoDeploymentConstants.AutoDeploymentId,
            DisplayName = "Auto deployment"
        };

        foreach (var environment in trigger.Environments)
        {
            await serviceDeploymentExecutor.DeployAsync(
                repositoryName,
                imageTag,
                environment,
                userDetails,
                cancellationToken);
        }
    }
}