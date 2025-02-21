using System.Collections.Immutable;
using System.Text.RegularExpressions;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Aws.Deployments;

public class TaskStateChangeEventHandler(
    IOptions<EcsEventListenerOptions> config,
    IEnvironmentLookup environmentLookup,
    IDeploymentsServiceV2 deploymentsService,
    IDeployableArtifactsService deployableArtifactsService,
    ITestRunService testRunService,
    ILogger<TaskStateChangeEventHandler> logger)
{
    private readonly List<string> _containersToIgnore = config.Value.ContainerToIgnore;

    private static readonly ISet<string> failureReasonsToIgnore = ImmutableHashSet.Create("UserInitiated", "EssentialContainerExited", "ServiceSchedulerInitiated");

    public async Task Handle(string id, EcsTaskStateChangeEvent ecsTaskStateChangeEvent,
        CancellationToken cancellationToken)
    {
        var env = environmentLookup.FindEnv(ecsTaskStateChangeEvent.Account);
        if (env == null)
        {
            logger.LogError(
                "Unable to convert {DeploymentId} to a deployment event, unknown environment/account: {Account} check the mappings!",
                ecsTaskStateChangeEvent.DeploymentId, ecsTaskStateChangeEvent.Account);
            return;
        }

        var artifact = await FindArtifact(ecsTaskStateChangeEvent, cancellationToken);

        if (artifact == null)
        {
            var containerList = string.Join(",", ecsTaskStateChangeEvent.Detail.Containers.Select(c => c.Image));
            logger.LogWarning("No known artifact found for task {id}, [{containers}]", id, containerList);
            return;
        }

        if (artifact.RunMode == ArtifactRunMode.Service.ToString().ToLower())
        {
            await UpdateDeployment(ecsTaskStateChangeEvent, cancellationToken);
            return;
        }

        if (artifact.RunMode == ArtifactRunMode.Job.ToString().ToLower())
        {
            await UpdateTestSuite(ecsTaskStateChangeEvent, artifact, cancellationToken);
            return;
        }


        logger.LogWarning("Artifact {artifactName} was not a known runMode {runMode}", artifact.ServiceName,
            artifact.RunMode);
    }


    /**
     * Handle events related to a deployed microservice
     */
    public async Task UpdateDeployment(EcsTaskStateChangeEvent ecsTaskStateChangeEvent,
        CancellationToken cancellationToken)
    {
        try
        {
            var lambdaId = ecsTaskStateChangeEvent.Detail.StartedBy.Trim();
            var instanceTaskId = ecsTaskStateChangeEvent.Detail.TaskArn;
            logger.LogInformation("Starting UpdateDeployment for {LambdaId}, instance {InstanceId}", lambdaId,
                instanceTaskId);

            // find the original requested deployment by the lambda id
            var deployment = await deploymentsService.FindDeploymentByLambdaId(lambdaId, cancellationToken);

            if (deployment == null)
            {
                // Fallback to matching on the most recent for that container/version
                var taskDefArn = ecsTaskStateChangeEvent.Detail.TaskDefinitionArn;
                deployment = await deploymentsService.FindDeploymentByTaskArn(taskDefArn, cancellationToken);

                if (deployment == null)
                {
                    logger.LogWarning(
                        "Failed to find a matching deployment for ecs deployment id {LambdaId} or {TaskDefArn}, it may have been triggered by a different instance of portal",
                        lambdaId,
                        taskDefArn);
                    return;
                }

                logger.LogWarning("Falling back to matching on Task-Definition Arn {CdpId} -> {TaskDefArn}",
                    deployment.CdpDeploymentId, taskDefArn);
            }

            var instanceStatus = DeploymentStatus.CalculateStatus(ecsTaskStateChangeEvent.Detail.DesiredStatus,
                ecsTaskStateChangeEvent.Detail.LastStatus);
            if (instanceStatus == null)
            {
                logger.LogWarning("Skipping unknown status for desired:{desired}, last:{last}",
                    ecsTaskStateChangeEvent.Detail.DesiredStatus, ecsTaskStateChangeEvent.Detail.LastStatus);
                return;
            }

            // Update the specific instance status
            logger.LogInformation(
                "Updating instance status for cdpID: {cdpId}, lambdaId: {lambdaId} instance {instanceId}, {msgId}",
                deployment.CdpDeploymentId, lambdaId, instanceTaskId, ecsTaskStateChangeEvent.DeploymentId);
            deployment.Instances[instanceTaskId] =
                new DeploymentInstanceStatus(instanceStatus, ecsTaskStateChangeEvent.Timestamp);

            // Limit the number of stopped service in the event of a crash-loop
            deployment.TrimInstance(50);

            // update the overall status
            deployment.Status = DeploymentStatus.CalculateOverallStatus(deployment);
            deployment.Unstable = DeploymentStatus.IsUnstable(deployment);
            deployment.Updated = ecsTaskStateChangeEvent.Timestamp;

            deployment.TaskDefinitionArn = ecsTaskStateChangeEvent.Detail.TaskArn;

            if (deployment.FailureReasons.Count == 0)
            {
                deployment.FailureReasons = ExtractFailureReasons(ecsTaskStateChangeEvent);
            }

            await deploymentsService.UpdateDeployment(deployment, cancellationToken);
            logger.LogInformation("Updated deployment {id}, {status}", deployment.LambdaId, deployment.Status);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to update deployment: {Message}", ex.Message);
        }
    }

    /**
     * Handle events related to a test suite. Unlike a service these are expected to run then exit.
     */
    public async Task UpdateTestSuite(EcsTaskStateChangeEvent ecsTaskStateChangeEvent, DeployableArtifact artifact,
        CancellationToken cancellationToken)
    {
        try
        {
            // TODO: have an allow-list of events we can process
            var env = environmentLookup.FindEnv(ecsTaskStateChangeEvent.Account);

            var taskArn = ecsTaskStateChangeEvent.Detail.TaskArn;

            // see if we've already linked a test run to the arn
            var testRun = await testRunService.FindByTaskArn(taskArn, cancellationToken);

            // if it's not there, find a candidate to link it to
            if (testRun == null)
            {
                logger.LogInformation("trying to link {id}", artifact.ServiceName);
                testRun = await testRunService.Link(
                    new TestRunMatchIds(artifact.ServiceName!, env!, ecsTaskStateChangeEvent.Timestamp), 
                    artifact,
                    taskArn, 
                    cancellationToken);
            }

            // if the linking fails, we have nothing to write the data to so bail
            if (testRun == null)
            {
                logger.LogWarning("Failed to find any test job for event {taskArn}", taskArn);
                return;
            }

            // use the container exit code to figure out if the tests passed. non-zero exit code == failure. 
            // TODO: this might not work in every case, other options are to parse the results from s3 when job is done
            // TODO: use sha256 instead once data is fixed
            var container = ecsTaskStateChangeEvent.Detail.Containers.FirstOrDefault(c => c.Name == artifact.Repo);
            var testResults = GenerateTestSuiteStatus(container);
            var failureReasons = ExtractFailureReasons(ecsTaskStateChangeEvent);
            var taskStatus = GenerateTestSuiteTaskStatus(ecsTaskStateChangeEvent.Detail.DesiredStatus,
                ecsTaskStateChangeEvent.Detail.LastStatus, failureReasons.Count > 0);
            
            logger.LogInformation("Updating {name} test-suite {runId} status to {status}:{result}", testRun.TestSuite,
                testRun.RunId, taskStatus, testResults);
            await testRunService.UpdateStatus(taskArn, taskStatus, testResults, ecsTaskStateChangeEvent.Timestamp,
                failureReasons, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError("Failed to update test suite: {ex}", ex);
        }
    }

    /**
     * Find the artifact belonging to an ECS event by matching the non-sidecar ECS container.
     */
    private async Task<DeployableArtifact?> FindArtifact(EcsTaskStateChangeEvent ecsTaskStateChangeEvent,
        CancellationToken cancellationToken)
    {
        foreach (var ecsContainer in ecsTaskStateChangeEvent.Detail.Containers)
        {
            var artifact = await FindArtifactBySha256(ecsContainer, cancellationToken) ??
                           await FindArtifactByTag(ecsContainer, cancellationToken);

            if (artifact == null) continue;
            if (_containersToIgnore.Contains(artifact.Repo))
            {
                logger.LogDebug("skipping ignored {repo} {tag}, {sha256}", artifact.Repo, artifact.Tag,
                    artifact.Sha256);
                continue;
            }

            logger.LogDebug("found artifact {repo} {tag}, {sha256}", artifact.Repo, artifact.Tag, artifact.Sha256);
            return artifact;
        }

        return null;
    }

    private async Task<DeployableArtifact?> FindArtifactByTag(EcsContainer ecsContainer,
        CancellationToken cancellationToken)
    {
        var (repo, tag) = SplitImage(ecsContainer.Image);
        if (string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(tag))
        {
            return null;
        }

        var artifact = tag switch
        {
            // We don't store the latest tag, so we just the highest semver
            "latest" => await deployableArtifactsService.FindLatest(repo, cancellationToken),
            _ => await deployableArtifactsService.FindByTag(repo, tag, cancellationToken)
        };

        return artifact;
    }

    private async Task<DeployableArtifact?> FindArtifactBySha256(EcsContainer ecsContainer,
        CancellationToken cancellationToken)
    {
        // Ideally use the Image Digest field
        var digest = ecsContainer.ImageDigest;

        if (!string.IsNullOrWhiteSpace(digest))
        {
            return await deployableArtifactsService.FindBySha256(digest, cancellationToken);
        }

        // Second instances for some reason don't have the image tag, but the sha, so use that
        var (_, sha256) = SplitSha(ecsContainer.Image);
        if (!string.IsNullOrWhiteSpace(sha256))
        {
            return await deployableArtifactsService.FindBySha256(sha256, cancellationToken);
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

    /**
     * Extract the name and tag from a full docker image url
     */
    public static (string?, string?) SplitSha(string image)
    {
        var rx = new Regex("^.+\\/(.+)@(.+)$");
        var result = rx.Match(image);
        if (result.Groups.Count == 3) return (result.Groups[1].Value, result.Groups[2].Value);

        return (null, null);
    }

 
    public static List<FailureReason> ExtractFailureReasons(EcsTaskStateChangeEvent ecsEvent)
    {
        var failureReasons = ecsEvent.Detail.Containers
            .Where(c => c.Reason != null)
            .Select(c => new FailureReason(c.Name, c.Reason!))
            .ToList();

        // Check if it was the timeout container that killed the test run.
        // In most cases it the exit code will be 143 (force killed by ECS), but when it fires the exit code would be <= 1. (TBC)
        var timeoutContainer = ecsEvent.Detail.Containers.FirstOrDefault(c => c.Name.EndsWith("-timeout"));
        if (timeoutContainer is { LastStatus: "STOPPED", ExitCode: <= 1 })
        {
            failureReasons.Add(new FailureReason(timeoutContainer.Name, "Test suite exceeded maximum run time"));
        }

        // Find any non-standard task level exit codes filtering out expected codes
        // EssentialContainerExited - i.e. tests have finished
        // UserInitiated - killed via the kill button
        if (ecsEvent.Detail is { StopCode: not null, StoppedReason: not null, })
        {
            if (!failureReasonsToIgnore.Contains(ecsEvent.Detail.StopCode))
            {
                failureReasons.Add(new FailureReason("ECS Task", ecsEvent.Detail.StoppedReason));
            }
        }

        return failureReasons;
    }

    /**
     * Interpret the status of the test suit based on the exit code of the test container
     */
    public static string? GenerateTestSuiteStatus(EcsContainer? container)
    {
        return container?.ExitCode switch
        {
            null => null,
            0 => "passed",
            _ => "failed"
        };
    }

    /**
  * Interpret the overall status of the test run's ECS task
  */
    public static string GenerateTestSuiteTaskStatus(string desired, string last, bool hasFailures)
    {
        if (hasFailures) return "failed";
        return desired switch
        {
            "RUNNING" => last switch
            {
                "PROVISIONING" => "starting",
                "PENDING" => "starting",
                "STOPPED" => "failed",
                _ => "in-progress"
            },
            "STOPPED" => last switch
            {
                "DEPROVISIONING" => "finished",
                "STOPPED" => "finished",
                _ => "stopping"
            },
            _ => "unknown"
        };
    }
}