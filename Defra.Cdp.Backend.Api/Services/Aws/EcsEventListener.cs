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

namespace Defra.Cdp.Backend.Api.Services.Aws;

public class EcsEventListener : SqsListener
{
    private readonly List<string> _containersToIgnore;
    private readonly IDeployablesService _deployablesService;
    private readonly IDeploymentsService _deploymentsService;
    private readonly EnvironmentLookup _environmentLookup;
    private readonly IEventsService _eventsService;
    private readonly ILogger<EcsEventListener> _logger;

    public EcsEventListener(IAmazonSQS sqs,
        IOptions<EcsEventListenerOptions> config,
        IDeploymentsService deploymentsService,
        EnvironmentLookup environmentLookup,
        IDeployablesService deployablesService,
        IEventsService eventsService,
        ILogger<EcsEventListener> logger) : base(sqs, config.Value.QueueUrl)
    {
        _deploymentsService = deploymentsService;
        _environmentLookup = environmentLookup;
        _logger = logger;
        _deployablesService = deployablesService;
        _eventsService = eventsService;
        _containersToIgnore = config.Value.ContainerToIgnore;
        _logger.LogInformation("Listening for deployment events on {}", config.Value.QueueUrl);
    }

    private async Task<List<Deployment>> ConvertToDeployment(EcsEvent ecsEvent)
    {
        var deployments = new List<Deployment>();
        var env = _environmentLookup.FindEnv(ecsEvent.Account);
        var containersToScan = ecsEvent.Detail.Containers.Where(c => !_containersToIgnore.Contains(c.Name));

        if (env == null)
        {
            _logger.LogError(
                "Unable to convert {} to a deployment event, unknown environment/account: {} check the mappings!",
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
                    _logger.LogInformation("Ignoring {}, not a known container", ecsContainer.Image);
                    continue;
                }

                var ids = await _deployablesService.FindByTag(repo, tag);
                if (ids == null)
                {
                    _logger.LogInformation("Ignoring {}, not a known container", ecsContainer.Image);
                    continue;
                }

                deployments.Add(new Deployment(
                    null,
                    ecsEvent.DeploymentId,
                    env,
                    ids.ServiceName ?? "unknown",
                    ids.Tag,
                    "TestUser", // TODO: work out where we get the user from 
                    ecsEvent.Detail.CreatedAt,
                    ecsContainer.LastStatus,
                    ecsContainer.Image,
                    ecsEvent.Detail.TaskDefinitionArn
                ));
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to connect to cdp-deployables, {}", ex);
            }

        return deployments;
    }

    private (string?, string?) SplitImage(string image)
    {
        var rx = new Regex("^.+\\/(.+):(.+)$");
        var result = rx.Match(image);
        if (result.Groups.Count == 3) return (result.Groups[1].Value, result.Groups[2].Value);

        return (null, null);
    }

    private async Task ProcessMessageAsync(string id, string messageBody)
    {
        var ecsEvent = JsonSerializer.Deserialize<EcsEvent>(messageBody);

        // TODO: consider tracking destroy etc so we can mark the end date on the deployment
        if (ecsEvent is { DetailType: "ECS Task State Change", Detail.DesiredStatus: "RUNNING" })
        {
            var deployments = await ConvertToDeployment(ecsEvent);
            foreach (var deployment in deployments)
            {
                _logger.LogInformation("saving deployment event {}:{}:{}", deployment.Environment,
                    deployment.Service, deployment.Version);
                await _deploymentsService.Insert(deployment);
            }
        }
        else
        {
            if (ecsEvent?.Detail == null)
                _logger.LogInformation("Not processing {}, details was null. message was {}", id,
                    messageBody);
            else
                _logger.LogInformation("Not processing {}, detail type was {} last status was {}",
                    id, ecsEvent.DetailType, ecsEvent.Detail.LastStatus);
        }
    }

    public override async Task HandleMessageAsync(Message message)
    {
        _logger.LogInformation("Receive: {MessageMessageId}: {MessageBody}", message.MessageId, message.Body);
        // keep a backup copy of the event (currently for debug/testing/replaying)
        await _eventsService.SaveMessage(message.MessageId, message.Body);
        await ProcessMessageAsync(message.MessageId, message.Body);
    }

    public async Task Backfill()
    {
        _logger.LogInformation("Starting backfill");
        var cursor = await _eventsService.FindAll();
        await cursor.ForEachAsync(async m =>
        {
            _logger.LogInformation("Backfilling {}", m.MessageId);
            try
            {
                await ProcessMessageAsync(m.MessageId, m.Body);
            }
            catch (Exception ex)
            {
                _logger.LogError("Backfill for message {} failed: {}", m.MessageId, ex);
            }
        });
        _logger.LogInformation("Finished backfill");
    }
}