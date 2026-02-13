using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.AutoDeploymentTriggers;
using Defra.Cdp.Backend.Api.Services.Dependencies;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Utils;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public class EcrEventListener(
    IAmazonSQS sqs,
    EcrEventHandler eventHandler,
    IOptions<EcrEventListenerOptions> config,
    IEcrEventsService ecrEventService,
    ILogger<EcrEventListener> logger)
    : SqsListener(sqs, config.Value.QueueUrl, logger)
{
    protected override async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Received message: {MessageId}", message.MessageId);

        try
        {
            await ecrEventService.SaveMessage(message.MessageId, message.Body, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist event");
        }

        try
        {
            await eventHandler.Handle(message.MessageId, message.Body, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process ECR message for event {MessageId}, {Error}", message.MessageId, ex.Message);
        }
    }
}

public class EcrEventHandler(
        IDeployableArtifactsService artifactsService,
        IAutoDeploymentTriggerExecutor autoDeploymentTriggerExecutor,
        ISbomEcrEventHandler sbomEcrEventHandler,
        ILogger<EcrEventListener> logger)
{
    public async Task Handle(string id, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting processing ECR Event {Id}", id);
        
        var ecrEvent = JsonSerializer.Deserialize<SqsEcrEvent>(body);
        
        if (ecrEvent?.Detail == null)
        {
            logger.LogInformation("Not processing {Id}, failed to process json", id);
            return;
        }

        if (ecrEvent.Detail.Result != "SUCCESS")
        {
            logger.LogInformation("Skipping non-success ECR message");
            return;
        }

        switch (ecrEvent.Detail.ActionType)
        {
            case "PUSH" when !SemVer.IsSemVer(ecrEvent.Detail.ImageTag):
                // Portal only accepts semver tagged artifacts
                logger.LogInformation("Not processing {Id}, tag [{ImageTag}] is not semver", id, ecrEvent.Detail.ImageTag);
                break;
            case "PUSH":
                logger.LogInformation("Processing {Sha256} ({Repo}:{Tag})", ecrEvent.Detail.ImageDigest, ecrEvent.Detail.RepositoryName,ecrEvent.Detail.ImageTag);
                await PersistArtifact(ecrEvent, cancellationToken);
                await sbomEcrEventHandler.Handle(cancellationToken);
                await autoDeploymentTriggerExecutor.Handle(ecrEvent.Detail.RepositoryName, ecrEvent.Detail.ImageTag, cancellationToken);
                break;
            case "DELETE":
                var deleted = await artifactsService.RemoveAsync(ecrEvent.Detail.RepositoryName, ecrEvent.Detail.ImageTag, cancellationToken);
                logger.LogInformation("Deleted {Sha256} ({Repo}:{Tag}) {Result}", ecrEvent.Detail.ImageDigest, ecrEvent.Detail.RepositoryName, ecrEvent.Detail.ImageTag, deleted ? "OK" : "FAILED");
                break;
            default:
                logger.LogInformation("Not processing {id}, message is not a PUSH or DELETE event", id);
                break;
        }
    }

    private async Task PersistArtifact(SqsEcrEvent ecrEvent, CancellationToken cancellationToken)
    {
        try
        {
            var artifact = DeployableArtifact.FromEcrEvent(ecrEvent);
            await artifactsService.CreateAsync(artifact, cancellationToken);
            logger.LogInformation("Persisted artifact {Repo}:{Tag}", ecrEvent.Detail.RepositoryName, ecrEvent.Detail.ImageTag);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            // Currently we only store sem-ver artifacts, we may reconsider thing going forward 
            logger.LogWarning("{Repo}:{Tag} is not semver, skipping ({Msg})", ecrEvent.Detail.RepositoryName, ecrEvent.Detail.ImageTag, ex.Message);
        }
    }
}