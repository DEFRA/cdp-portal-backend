using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Utils;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public class EcrEventListener(
    IAmazonSQS sqs,
    IArtifactScanner docker,
    IOptions<EcrEventListenerOptions> config,
    IEcrEventsService ecrEventService,
    ILogger<EcrEventListener> logger)
    : SqsListener(sqs, config.Value.QueueUrl, logger)
{
    private readonly EcrEventListenerOptions _options = config.Value;
    private readonly IAmazonSQS _sqs = sqs;
    
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
            var result = await HandleEcrMessage(message.MessageId, message.Body, cancellationToken);
            if (result is { Success: true, Artifact: not null })
                logger.LogInformation(
                    "Processed {MsgMessageId}, image ${ResultSha256} ({ResultRepo}:{ResultTag}) scanned ok",
                    message.MessageId, result.Artifact.Sha256, result.Artifact.Repo, result.Artifact.Tag);
            else
                logger.LogInformation("Skipping processing of {MessageId}, {Error}", message.MessageId, result.Error);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to scan image for event {MessageId}", message.MessageId);
        }

        // TODO: better error detection to decide if we delete, dead letter or retry...
        await _sqs.DeleteMessageAsync(_options.QueueUrl, message.ReceiptHandle, cancellationToken);
    }
    
    
    private async Task<ArtifactScannerResult> HandleEcrMessage(string id, string body, CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting processing ECR Event {Id}", id);
        // AWS JSON messages are sent in with their " escaped (\"), in order to parse, they must be unescaped
        var ecrEvent = JsonSerializer.Deserialize<SqsEcrEvent>(body);

        // Only scan push event for images that have a semver tag (i.e. ignore latest and anything else)
        if (ecrEvent?.Detail == null)
        {
            logger.LogInformation("Not processing {Id}, failed to process json", id);
            return ArtifactScannerResult.Failure($"Not processing {id}, failed to process json");
        }

        if (ecrEvent.Detail.Result != "SUCCESS")
            return ArtifactScannerResult.Failure($"Not processing {id}, result is not a SUCCESS");

        if (ecrEvent.Detail.ActionType != "PUSH")
            return ArtifactScannerResult.Failure($"Not processing {id}, message is not a PUSH event");

        if (!SemVer.IsSemVer(ecrEvent.Detail.ImageTag))
        {
            logger.LogInformation("Not processing {Id}, tag [{ImageTag}] is not semver", id, ecrEvent.Detail.ImageTag);
            // TODO: have a better return type that can indicate why it wasn't scanned.
            return ArtifactScannerResult.Failure(
                $"Not processing {id}, tag [{ecrEvent.Detail.ImageTag}] is not semver");
        }

        return await docker.ScanImage(ecrEvent.Detail.RepositoryName, ecrEvent.Detail.ImageTag, cancellationToken);
    }
}