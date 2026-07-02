using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Utils.Auditing;
using Defra.Cdp.Backend.Api.Utils.Clients;

namespace Defra.Cdp.Backend.Api.Services.Deployments;

public interface IServiceDeploymentExecutor
{
    Task DeployAsync(
        string repositoryName,
        string imageTag,
        string environment,
        UserDetails user,
        CancellationToken cancellationToken);
}

public class ServiceDeploymentExecutor(
    IDeploymentsService deploymentsService,
    ISelfServiceOpsClient selfServiceOpsClient,
    IAppConfigVersionsService appConfigVersionsService,
    ILogger<ServiceDeploymentExecutor> logger) : IServiceDeploymentExecutor
{
    public async Task DeployAsync(
        string repositoryName,
        string imageTag,
        string environment,
        UserDetails user,
        CancellationToken cancellationToken)
    {
        var deploymentSettings =
            await deploymentsService.FindDeploymentSettings(repositoryName, environment, cancellationToken);
        if (deploymentSettings == null)
        {
            logger.LogError(
                "Could not find deployment settings for repository {RepositoryName} in environment {Environment}",
                repositoryName, environment);
            return;
        }

        var configVersion =
            await appConfigVersionsService.FindLatestAppConfigVersion(environment, cancellationToken);
        if (configVersion == null)
        {
            logger.LogError("Could not find latest config version for environment: {Environment}", environment);
            return;
        }

        logger.LogInformation(
            "Auto-deploying {RepositoryName} version {ImageTag} to {Environment}",
            repositoryName, imageTag, environment);

        await selfServiceOpsClient.AutoDeployService(
            repositoryName,
            imageTag,
            environment,
            user,
            deploymentSettings,
            configVersion.CommitSha,
            cancellationToken);

        logger.Audit("Auto-deploying {Repo}:{Version} to {Environment}", repositoryName, imageTag, environment);
    }
}
