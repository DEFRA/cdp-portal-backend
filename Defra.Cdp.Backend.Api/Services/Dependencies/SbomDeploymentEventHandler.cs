using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.Deployments;

namespace Defra.Cdp.Backend.Api.Services.Dependencies;

public interface ISbomDeploymentEventHandler
{
    public Task Handle(string id, EcsDeploymentStateChangeEvent ecsEvent, CancellationToken ct);
}


public class SbomDeploymentEventHandler(ISbomExplorerClient client, IDeploymentsService dependencyService, IEnvironmentLookup envLookup, ILogger<SbomDeploymentEventHandler> logger) : ISbomDeploymentEventHandler
{
    public async Task Handle(string id, EcsDeploymentStateChangeEvent ecsEvent, CancellationToken ct)
    {
        try
        {
            var env = envLookup.FindEnv(ecsEvent.Account);
            if (env == null)
            {
                logger.LogWarning("No environment matching account {Account} event id {Id}", ecsEvent.Account, id);
                return;
            }

            var deployments =
                await dependencyService.RunningDeploymentsForService(new DeploymentMatchers { Environment = env }, ct);
            await client.PushRunningServices(env, deployments, ct);
            logger.LogWarning("Pushed {Size} deployments for {Env} to cdp-sbom-explorer-backend", deployments.Count, env);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to push update to cdp-sbom-explorer-backend");
        }
    }
}