using System.Text.RegularExpressions;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Aws.Deployments;

public class DeploymentEventHandlerV2
{
    private readonly List<string> _containersToIgnore;
    private readonly IDeployablesService _deployablesService;
    private readonly IDeploymentsServiceV2 _deploymentsService;
    private readonly ITestRunService _testRunService;
    private readonly IEnvironmentLookup _environmentLookup;
    private readonly ILogger<DeploymentEventHandler> _logger;

    public DeploymentEventHandlerV2(
        IOptions<EcsEventListenerOptions> config,
        IEnvironmentLookup environmentLookup,
        IDeploymentsServiceV2 deploymentsService,
        IDeployablesService deployablesService,
        ITestRunService testRunService,
        ILogger<DeploymentEventHandler> logger)
    {
        _deploymentsService = deploymentsService;
        _environmentLookup = environmentLookup;
        _logger = logger;
        _deployablesService = deployablesService;
        _testRunService = testRunService;
        _containersToIgnore = config.Value.ContainerToIgnore;
    }

    public async Task Handle(string id, EcsEvent ecsEvent, CancellationToken cancellationToken)
    {
        var env = _environmentLookup.FindEnv(ecsEvent.Account);
        if (env == null)
        {
            _logger.LogError(
                "Unable to convert {DeploymentId} to a deployment event, unknown environment/account: {Account} check the mappings!",
                ecsEvent.DeploymentId, ecsEvent.Account);
            return;
        }

        var artifact = await FindArtifact(ecsEvent, cancellationToken);

        if (artifact == null)
        {
            var containerList = string.Join(",", ecsEvent.Detail.Containers.Select(c => c.Image));
            _logger.LogWarning("No known artifact found for task {id}, [{containers}]", id, containerList);
            return;
        }
        
        if (artifact.RunMode == ArtifactRunMode.Service.ToString().ToLower())
        {
            await UpdateDeployment(ecsEvent, artifact, cancellationToken);
            return;
        }

        // TODO: when we're ready to migrate to V2, move the test run handler here and uncomment
        /*
        if (artifact.RunMode == ArtifactRunMode.Job.ToString().ToLower())
        {
            await UpdateTestSuite(ecsEvent, artifact, cancellationToken);
            return;
        }
        */
        
        _logger.LogWarning("Artifact {artifactName} was not a known runMode {runMode}", artifact.ServiceName, artifact.RunMode);
    }
    
    
    /**
     * Handle events related to a deployed microservice
     */
    public async Task UpdateDeployment(EcsEvent ecsEvent, DeployableArtifact artifact, CancellationToken cancellationToken)
    {
        try
        {
            var lambdaId = ecsEvent.Detail.StartedBy.Trim();
            var env = _environmentLookup.FindEnv(ecsEvent.Account);
            var deployedAt = ecsEvent.Timestamp;
            var taskId = ecsEvent.Detail.TaskDefinitionArn;
            var instanceTaskId = ecsEvent.Detail.TaskArn;

            // find the original requested deployment by the lambda id
            var deployment = await _deploymentsService.FindDeploymentByLambdaId(lambdaId, cancellationToken);

            if (deployment == null)
            {
                _logger.LogWarning($"Failed to find a matching deployment for {lambdaId}, it may have been triggered by a different instance of portal", lambdaId);
                return;
            }

            // TODO: match on sha256 instead
            var container = ecsEvent.Detail.Containers.FirstOrDefault(c => c.Image.EndsWith(artifact.Repo + ":" + artifact.Tag));
            if (container == null)
            {
                throw new Exception( $"Failed to find the ECS container entry for {artifact.Repo}:{artifact.Tag}");
            }
            
            var instanceStatus = DeploymentStatus.CalculateStatus(ecsEvent.Detail.DesiredStatus, ecsEvent.Detail.LastStatus);
            if (instanceStatus == null)
            {
                _logger.LogWarning("Skipping unknown status for desired:{desired}, last{last}", ecsEvent.Detail.DesiredStatus, ecsEvent.Detail.LastStatus);
                return;
            }
            
            // Update the specific instance status
            if (!deployment.Instances.ContainsKey(instanceTaskId))
            {
                deployment.Instances[instanceTaskId] = new DeploymentInstanceStatus();
            }
            deployment.Instances[instanceTaskId].Status = instanceStatus;
            deployment.Instances[instanceTaskId].Updated = ecsEvent.Timestamp;  

            // update the overall status
            deployment.Status = DeploymentStatus.CalculateOverallStatus(deployment);
            deployment.Unstable = DeploymentStatus.IsUnstable(deployment);
            deployment.Updated = ecsEvent.Timestamp;

            await _deploymentsService.UpdateDeployment(deployment, cancellationToken);
            _logger.LogInformation("Updated deployment {id}, {status}", deployment.LambdaId, deployment.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to update deployment: {ex}", ex);
        }
    }
   
    /**
     * Find the artifact belonging to an ECS event by matching the non-sidecar ECS container.
     */
    private async Task<DeployableArtifact?> FindArtifact(EcsEvent ecsEvent, CancellationToken cancellationToken)
    {
        foreach (var ecsContainer in ecsEvent.Detail.Containers)
        {
            var (repo, tag) = SplitImage(ecsContainer.Image);
            if (string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(tag))
            {
                _logger.LogDebug("skipping empty {repo} {tag}", repo, tag);
                continue;
            }

            if (_containersToIgnore.Contains(repo))
            {
                _logger.LogDebug("skipping ignored {repo} {tag}", repo, tag);
                continue;
            }
            _logger.LogDebug("looking for container {repo} {tag}", repo, tag);

            
            var artifact = tag switch
            {
                // We don't store the latest tag, so we just the highest semver
                "latest" => await _deployablesService.FindLatest(repo, cancellationToken), // TODO: once we fix the image hash, search on the image hash
                _        => await _deployablesService.FindByTag(repo, tag, cancellationToken)
            };

            if (artifact != null)
            {
                _logger.LogDebug("found artifact {hash} for {repo}:{tag}", artifact.Sha256, repo, tag);
                return artifact;
            }
        }
        return null;
    }
    
    /**
     * Extract the name and tag from a full docker image url 
     */
    public static (string?, string?) SplitImage(string image)
    {
        var rx = new Regex("^.+\\/(.+):(.+)$");
        var result = rx.Match(image);
        if (result.Groups.Count == 3) return (result.Groups[1].Value, result.Groups[2].Value);

        return (null, null);
    }
}