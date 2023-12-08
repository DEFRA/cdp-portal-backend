using System.Text.Json;
using System.Text.RegularExpressions;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Services.Tenants;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Deployment = Defra.Cdp.Backend.Api.Models.Deployment;
using Task = System.Threading.Tasks.Task;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public class EcsEventListener : SqsListener
{
    private const string Requested = "REQUESTED";
    private readonly List<string> _containersToIgnore;
    private readonly IDeployablesService _deployablesService;
    private readonly IDeploymentsService _deploymentsService;
    private readonly IEcsEventsService _ecsEventsService;
    private readonly EnvironmentLookup _environmentLookup;
    private readonly ILogger<EcsEventListener> _logger;

    public EcsEventListener(IAmazonSQS sqs,
        IOptions<EcsEventListenerOptions> config,
        IDeploymentsService deploymentsService,
        EnvironmentLookup environmentLookup,
        IDeployablesService deployablesService,
        IEcsEventsService ecsEventsService,
        ILogger<EcsEventListener> logger) : base(sqs, config.Value.QueueUrl)
    {
        _deploymentsService = deploymentsService;
        _environmentLookup = environmentLookup;
        _logger = logger;
        _deployablesService = deployablesService;
        _ecsEventsService = ecsEventsService;
        _containersToIgnore = config.Value.ContainerToIgnore;
        _logger.LogInformation("Listening for deployment events on {QueueUrl}", config.Value.QueueUrl);
    }

    private async Task UpdateDeploymentIds(EcsEvent ecsEvent, CancellationToken cancellationToken)
    {
        var cdpDeploymentId = ecsEvent.CdpDeploymentId;
        var ecsSvcDeploymentId = ecsEvent.Detail.EcsSvcDeploymentId?.Trim();

        if (!string.IsNullOrWhiteSpace(cdpDeploymentId) && !string.IsNullOrWhiteSpace(ecsSvcDeploymentId))
        {
            var requestedDeployment =
                await _deploymentsService.FindDeploymentByEcsSvcDeploymentId(ecsSvcDeploymentId,
                    cancellationToken);

            var d = await _deploymentsService.FindDeployment(cdpDeploymentId, cancellationToken);

            if (d != null)
            {
                _logger.LogInformation($"Matching id {cdpDeploymentId} to deployer {ecsSvcDeploymentId}");
                var updatedDeployment = new Deployment
                {
                    Id = d.Id,
                    DeploymentId = d.DeploymentId,
                    Environment = d.Environment,
                    Service = d.Service,
                    Version = d.Version,
                    User = d.User,
                    DeployedAt = d.DeployedAt,
                    Status = d.Status,
                    DockerImage = d.DockerImage,
                    TaskId = d.TaskId,
                    InstanceTaskId = d.InstanceTaskId,
                    InstanceCount = d.InstanceCount
                };
                await _deploymentsService.Insert(updatedDeployment, cancellationToken);
            }
            else
            {
                _logger.LogInformation(
                    $"couldn't find anything to match for {cdpDeploymentId} to deployer {ecsSvcDeploymentId} ");
            }
        }

        _logger.LogWarning("Could not match an ecs lambda deployment to an existing request");
    }

    private async Task<List<Deployment>> ConvertToDeployment(EcsEvent ecsEvent, CancellationToken cancellationToken)
    {
        var deployments = new List<Deployment>();
        var env = _environmentLookup.FindEnv(ecsEvent.Account);
        var containersToScan = ecsEvent.Detail.Containers.Where(c => !_containersToIgnore.Contains(c.Name));

        if (env == null)
        {
            _logger.LogError(
                "Unable to convert {DeploymentId} to a deployment event, unknown environment/account: {Account} check the mappings!",
                ecsEvent.DeploymentId, ecsEvent.Account);
            return new List<Deployment>();
        }

        foreach (var ecsContainer in containersToScan)
            try
            {
                // skip any container that isn't know to deployables.
                var (repo, tag) = SplitImage(ecsContainer.Image);
                if (string.IsNullOrWhiteSpace(repo) || string.IsNullOrWhiteSpace(tag))
                {
                    _logger.LogInformation("Ignoring {Image}, could not extract repo and tag, not a known container",
                        ecsContainer.Image);
                    continue;
                }

                var ids = await _deployablesService.FindByTag(repo, tag, cancellationToken);
                if (ids == null)
                {
                    _logger.LogInformation("Ignoring {Image}, not a known container", ecsContainer.Image);
                    continue;
                }

                var deployedAt = ecsEvent.Timestamp;
                var taskId = ecsEvent.Detail.TaskDefinitionArn;
                var instanceTaskId = ecsEvent.Detail.TaskArn;
                var deploymentId = ecsEvent.DeploymentId;

                // Find the requested deployment so we can fill out the username
                var requestedDeployment =
                    await _deploymentsService.FindDeploymentByEcsSvcDeploymentId(ecsEvent.Detail.StartedBy.Trim(),
                        cancellationToken);

                var deployment = new Deployment
                {
                    DeploymentId = deploymentId,
                    Environment = env,
                    Service = ids.ServiceName ?? "unknown",
                    Version = ids.Tag,
                    User = requestedDeployment?.User ?? "n/a",
                    DeployedAt = deployedAt,
                    Status = ecsContainer.LastStatus,
                    DockerImage = ecsContainer.Image,
                    TaskId = taskId,
                    InstanceTaskId = instanceTaskId,
                    InstanceCount = requestedDeployment?.InstanceCount,
                    EcsSvcDeploymentId = ecsEvent.Detail.StartedBy
                };

                if (requestedDeployment is { DeploymentId: null, Id: not null })
                {
                    _logger.LogInformation("Linking {Id} to deployment {DeploymentId}", requestedDeployment.Id,
                        deploymentId);
                    await _deploymentsService.LinkRequestedDeployment(requestedDeployment.Id, deployment,
                        cancellationToken);
                }

                deployments.Add(deployment);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to connect to cdp-deployables");
            }

        return deployments;
    }

    public static (string?, string?) SplitImage(string image)
    {
        var rx = new Regex("^.+\\/(.+):(.+)$");
        var result = rx.Match(image);
        if (result.Groups.Count == 3) return (result.Groups[1].Value, result.Groups[2].Value);

        return (null, null);
    }

    private async Task ProcessMessageAsync(string id, string messageBody, CancellationToken cancellationToken)
    {
        var ecsEvent = JsonSerializer.Deserialize<EcsEvent>(messageBody);

        // TODO: consider tracking destroy etc so we can mark the end date on the deployment
        if (ecsEvent is { DetailType: "ECS Task State Change", Detail.DesiredStatus: "RUNNING" })
        {
            var deployments = await ConvertToDeployment(ecsEvent, cancellationToken);
            foreach (var deployment in deployments)
            {
                _logger.LogInformation("saving deployment event {Environment}:{Service}:{Version}",
                    deployment.Environment,
                    deployment.Service, deployment.Version);
                await _deploymentsService.Insert(deployment, cancellationToken);
            }
        }
        else if (ecsEvent is { DetailType: "ECS Lambda Deployment Updated" })
        {
            await UpdateDeploymentIds(ecsEvent, cancellationToken);
        }
        else
        {
            if (ecsEvent?.Detail == null)
                _logger.LogInformation("Not processing {Id}, details was null. message was {MessageBody}", id,
                    messageBody);
            else
                _logger.LogInformation("Not processing {Id}, detail type was {DetailType} last status was {LastStatus}",
                    id, ecsEvent.DetailType, ecsEvent.Detail.LastStatus);
        }
    }

    public override async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Receive: {MessageMessageId}: {MessageBody}", message.MessageId, message.Body);
        // keep a backup copy of the event (currently for debug/testing/replaying)
        await _ecsEventsService.SaveMessage(message.MessageId, message.Body, cancellationToken);
        await ProcessMessageAsync(message.MessageId, message.Body, cancellationToken);
    }

    public async Task Backfill(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting backfill");
        var cursor = await _ecsEventsService.FindAll(cancellationToken);
        await cursor.ForEachAsync(async m =>
        {
            _logger.LogInformation("Backfilling {MessageId}", m.MessageId);
            try
            {
                await ProcessMessageAsync(m.MessageId, m.Body, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Backfill for message {MessageId} failed", m.MessageId);
            }
        });
        _logger.LogInformation("Finished backfill");
    }
}