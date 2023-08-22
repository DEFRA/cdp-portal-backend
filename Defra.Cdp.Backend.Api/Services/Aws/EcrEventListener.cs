using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Utils;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public class EcrEventListener
{
    private readonly IArtifactScanner _docker;
    private readonly IEcrEventsService _ecrEventService;
    private readonly ILogger _logger;
    private readonly EcrEventListenerOptions _options;

    private readonly IAmazonSQS _sqs;

    public EcrEventListener(IAmazonSQS sqs, IArtifactScanner docker, IOptions<EcrEventListenerOptions> config,
        IEcrEventsService ecrEventService,
        ILogger<EcrEventListener> logger)
    {
        _sqs = sqs;
        _docker = docker;
        _options = config.Value;
        _ecrEventService = ecrEventService;
        _logger = logger;
    }

    public async void ReadAsync()
    {
        _logger.LogInformation("Listening for events on {OptionsQueueUrl}", _options.QueueUrl);

        var falloff = 1;
        while (_options.Enabled)
            try
            {
                await GetMessages();
                falloff = 1;
            }
            catch (Exception e)
            {
                _logger.LogError(e.Message);
                Thread.Sleep(1000 * Math.Min(60, falloff));
                falloff++;
            }
    }

    private async Task GetMessages()
    {
        var response = await _sqs.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _options.QueueUrl, WaitTimeSeconds = _options.WaitTimeSeconds, MaxNumberOfMessages = 1
        });

        if (response == null) return;

        foreach (var msg in response.Messages)
        {
            if (msg == null) continue;

            _logger.LogInformation("Received message: {}", msg.MessageId);

            try
            {
                await _ecrEventService.SaveMessage(msg.MessageId, msg.Body);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to persist event: {}", ex);
            }

            try
            {
                var result = await ProcessMessage(msg.MessageId, msg.Body);
                if (result == null)
                    _logger.LogInformation("Skipping processing of {}", msg.MessageId);
                else
                    _logger.LogInformation(
                        "Processed {MsgMessageId}, image ${ResultSha256} ({ResultRepo}:{ResultTag}) scanned ok",
                        msg.MessageId, result.Sha256, result.Repo, result.Tag);
            }
            catch (Exception ex)
            {
                _logger.LogError("Failed to scan image for event {}: {}", msg.MessageId, ex);
            }

            // TODO: better error detection to decide if we delete, dead letter or retry...
            await _sqs.DeleteMessageAsync(_options.QueueUrl, msg.ReceiptHandle);
        }
    }

    public async Task<DeployableArtifact?> ProcessMessage(string id, string body)
    {
        // AWS JSON messages are sent in with their " escaped (\"), in order to parse, they must be unescaped
        var ecrEvent = JsonSerializer.Deserialize<SqsEcrEvent>(body);

        // Only scan push event for images that have a semver tag (i.e. ignore latest and anything else)
        if (ecrEvent?.Detail == null)
        {
            _logger.LogInformation("Not processing {}, failed to process json", id);
            throw new ImageProcessingException($"Not processing {id}, failed to process json");
        }

        if (ecrEvent.Detail.Result != "SUCCESS")
        {
            _logger.LogInformation("Not processing {}, result is not a SUCCESS", id);
            return null;
        }

        if (ecrEvent.Detail.ActionType != "PUSH")
        {
            _logger.LogInformation("Not processing {}, message is not a PUSH event", id);
            return null;
        }

        if (!SemVer.IsSemVer(ecrEvent.Detail.ImageTag))
        {
            _logger.LogInformation("Not processing {}, tag [{}] is not semver", id, ecrEvent.Detail.ImageTag);
            // TODO: have a better return type that can indicate why it wasnt scanned.
            return null;
        }

        return await _docker.ScanImage(ecrEvent.Detail.RepositoryName, ecrEvent.Detail.ImageTag);
    }
}