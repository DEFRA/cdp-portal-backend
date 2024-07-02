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
    private readonly ILogger<DeploymentEventHandlerV2> _logger;

    public DeploymentEventHandlerV2(
        IOptions<EcsEventListenerOptions> config,
        IEnvironmentLookup environmentLookup,
        IDeploymentsServiceV2 deploymentsService,
        IDeployablesService deployablesService,
        ITestRunService testRunService,
        ILogger<DeploymentEventHandlerV2> logger)
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

        if (artifact.RunMode == ArtifactRunMode.Job.ToString().ToLower())
        {
            await UpdateTestSuite(ecsEvent, artifact, cancellationToken);
            return;
        }
        
        
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
            var instanceTaskId = ecsEvent.Detail.TaskArn;
            _logger.LogInformation("Starting UpdateDeployment for {lambdaId}, instance {instanceId}", lambdaId, instanceTaskId);
            
            // find the original requested deployment by the lambda id
            var deployment = await _deploymentsService.FindDeploymentByLambdaId(lambdaId, cancellationToken);

            if (deployment == null)
            {
                _logger.LogWarning($"Failed to find a matching deployment for {lambdaId}, it may have been triggered by a different instance of portal", lambdaId);
                return;
            }
            
            var instanceStatus = DeploymentStatus.CalculateStatus(ecsEvent.Detail.DesiredStatus, ecsEvent.Detail.LastStatus);
            if (instanceStatus == null)
            {
                _logger.LogWarning("Skipping unknown status for desired:{desired}, last{last}", ecsEvent.Detail.DesiredStatus, ecsEvent.Detail.LastStatus);
                return;
            }
            
            // Update the specific instance status
            _logger.LogInformation("Updated deployment: {lambdaId} instance: {instanceId} status to {status}", deployment.LambdaId, instanceTaskId, instanceStatus);
            deployment.Instances[instanceTaskId] = new DeploymentInstanceStatus(instanceStatus, ecsEvent.Timestamp);
            await _deploymentsService.UpdateInstance(lambdaId, instanceTaskId, deployment.Instances[instanceTaskId], cancellationToken);
            
            // Limit the number of stopped service in the event of a crash-loop
            // deployment.TrimInstance(50);
            
            // update the overall status
            await _deploymentsService.UpdateDeploymentStatus(deployment.LambdaId!, ecsEvent.Timestamp, cancellationToken);
            _logger.LogInformation("Updated deployment {lambdaId} {status}", deployment.LambdaId, deployment.Status);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to update deployment: {ex}", ex);
        }
    }
   
    /**
     * Handle events related to a test suite. Unlike a service these are expected to run then exit. 
     */
    public async Task UpdateTestSuite(EcsEvent ecsEvent, DeployableArtifact artifact, CancellationToken cancellationToken)
    {
        try
        {
            // TODO: have an allow-list of events we can process
            var env = _environmentLookup.FindEnv(ecsEvent.Account);
            
            var taskArn = ecsEvent.Detail.TaskArn;

            // see if we've already linked a test run to the arn
            var testRun = await _testRunService.FindByTaskArn(taskArn, cancellationToken);

            // if its not there, find a candidate to link it to
            if (testRun == null)
            {
                _logger.LogInformation("trying to link {id}", artifact.ServiceName);
                testRun = await _testRunService.Link(
                    new TestRunMatchIds(artifact.ServiceName!, env!, ecsEvent.Timestamp), artifact, taskArn, cancellationToken);
            }
            
            // if the linking fails, we have nothing to write the data to so bail
            if (testRun == null)
            {
                _logger.LogWarning("Failed to find any test job for event {taskArn}", taskArn);
                return;
            }

            // use the container exit code to figure out if the tests passed. non-zero exit code == failure. 
            // TODO: this might not work in every case, other options are to parse the results from s3 when job is done
            // TODO: use sha256 instead once data is fixed
            var container = ecsEvent.Detail.Containers.FirstOrDefault(c => c.Name == artifact.Repo);
            var testResults = GenerateTestSuiteStatus(container);
            
            var taskStatus = GenerateTestSuiteTaskStatus(ecsEvent.Detail.DesiredStatus, ecsEvent.Detail.LastStatus);
            
            _logger.LogInformation("Updating {name} test-suite {runId} status to {status}:{result}", testRun.TestSuite,
                testRun.RunId, taskStatus, testResults);
            await _testRunService.UpdateStatus(taskArn, taskStatus, testResults, ecsEvent.Timestamp, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError("Failed to update test suite: {ex}", ex);
        }
    }
       
    /**
     * Interpret the status of the test suit based on the exit code of the test container
     */
       public static string? GenerateTestSuiteStatus(EcsContainer? container)
       {
           return container?.ExitCode switch
           {
               null => null,
               0    => "passed",
               _    => "failed"
           };
       }
    
       /**
     * Interpret the overall status of the test run's ECS task 
     */
       public static string GenerateTestSuiteTaskStatus(string desired, string last)
       {
           return desired switch
           {
               "RUNNING" => last switch
               {
                   "PROVISIONING" => "starting",
                   "PENDING"      => "starting",
                   "STOPPED"      => "failed",
                   _              => "in-progress"
               },
               "STOPPED" => last switch
               {
                   "DEPROVISIONING" => "finished", 
                   "STOPPED"        => "finished",
                   _                => "stopping"
               },
               _ => "unknown"
           };
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