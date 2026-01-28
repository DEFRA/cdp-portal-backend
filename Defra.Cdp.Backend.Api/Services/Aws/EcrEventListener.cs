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
        IArtifactScanner artifactScanner,
        IDeployableArtifactsService artifactsService,
        IAutoDeploymentTriggerExecutor autoDeploymentTriggerExecutor,
        ISbomEcrEventHandler sbomEcrEventHandler,
        ILogger<EcrEventListener> logger)
{
    public async Task Handle(string id, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting processing ECR Event {Id}", id);
        // AWS JSON messages are sent in with their " escaped (\"), in order to parse, they must be unescaped
        var ecrEvent = JsonSerializer.Deserialize<SqsEcrEvent>(body);

        // Only scan push event for images that have a semver tag (i.e. ignore latest and anything else)
        if (ecrEvent?.Detail == null)
        {
            logger.LogInformation("Not processing {Id}, failed to process json", id);
            return;
        }

        if (ecrEvent.Detail.Result != "SUCCESS")
        {
            logger.LogInformation("Processing ECR Event {Id}, failed to process json", id);
            return;
        }

        switch (ecrEvent.Detail.ActionType)
        {
            case "PUSH" when !SemVer.IsSemVer(ecrEvent.Detail.ImageTag):
                logger.LogInformation("Not processing {Id}, tag [{ImageTag}] is not semver", id, ecrEvent.Detail.ImageTag);
                break;
            case "PUSH":
                var scanResult = await artifactScanner.ScanImage(ecrEvent.Detail.RepositoryName, ecrEvent.Detail.ImageTag, cancellationToken);
                logger.LogInformation("Scanned {Sha256} ({Repo}:{Tag}) {Result}", scanResult.Artifact?.Sha256, scanResult.Artifact?.Repo, scanResult.Artifact?.Tag, scanResult.Success ? "OK" : "FAILED");
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
}