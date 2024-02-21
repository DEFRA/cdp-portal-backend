using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Utils;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public class EcrEventListener : SqsListener
{
    private readonly IArtifactScanner _docker;
    private readonly IEcrEventsService _ecrEventService;
    private readonly ILogger<EcrEventListener> _logger;
    private readonly EcrEventListenerOptions _options;

    private readonly IAmazonSQS _sqs;

    public EcrEventListener(IAmazonSQS sqs, IArtifactScanner docker, IOptions<EcrEventListenerOptions> config,
        IEcrEventsService ecrEventService,
        ILogger<EcrEventListener> logger)  : base(sqs, config.Value.QueueUrl, logger)
    {
        _sqs = sqs;
        _docker = docker;
        _options = config.Value;
        _ecrEventService = ecrEventService;
        _logger = logger;
    }

    public override async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received message: {MessageId}", message.MessageId);

        try
        {
            await _ecrEventService.SaveMessage(message.MessageId, message.Body, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist event");
        }

        try
        {
            var result = await HandleEcrMessage(message.MessageId, message.Body, cancellationToken);
            if (result.Success && result.Artifact != null)
                _logger.LogInformation(
                    "Processed {MsgMessageId}, image ${ResultSha256} ({ResultRepo}:{ResultTag}) scanned ok",
                    message.MessageId, result.Artifact.Sha256, result.Artifact.Repo, result.Artifact.Tag);
            else
                _logger.LogInformation("Skipping processing of {MessageId}, {Error}", message.MessageId, result.Error);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to scan image for event {MessageId}", message.MessageId);
        }

        // TODO: better error detection to decide if we delete, dead letter or retry...
        await _sqs.DeleteMessageAsync(_options.QueueUrl, message.ReceiptHandle, cancellationToken);
    }


    public async Task<ArtifactScannerResult> HandleEcrMessage(string id, string body, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting processing ECR Event {Id}", id);
        // AWS JSON messages are sent in with their " escaped (\"), in order to parse, they must be unescaped
        var ecrEvent = JsonSerializer.Deserialize<SqsEcrEvent>(body);

        // Only scan push event for images that have a semver tag (i.e. ignore latest and anything else)
        if (ecrEvent?.Detail == null)
        {
            _logger.LogInformation("Not processing {Id}, failed to process json", id);
            return ArtifactScannerResult.Failure($"Not processing {id}, failed to process json");
        }

        if (ecrEvent.Detail.Result != "SUCCESS")
            return ArtifactScannerResult.Failure($"Not processing {id}, result is not a SUCCESS");

        if (ecrEvent.Detail.ActionType != "PUSH")
            return ArtifactScannerResult.Failure($"Not processing {id}, message is not a PUSH event");

        if (!SemVer.IsSemVer(ecrEvent.Detail.ImageTag))
        {
            _logger.LogInformation("Not processing {Id}, tag [{ImageTag}] is not semver", id, ecrEvent.Detail.ImageTag);
            // TODO: have a better return type that can indicate why it wasn't scanned.
            return ArtifactScannerResult.Failure(
                $"Not processing {id}, tag [{ecrEvent.Detail.ImageTag}] is not semver");
        }

        return await _docker.ScanImage(ecrEvent.Detail.RepositoryName, ecrEvent.Detail.ImageTag, cancellationToken);
    }
}