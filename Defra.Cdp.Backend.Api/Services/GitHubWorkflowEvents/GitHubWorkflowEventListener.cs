using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents;

/**
 * Listens for events sent by GitHub Workflows
 * Messages are sent by the workflows and contain event specific payloads
 */
public class GitHubWorkflowEventListener(
    IAmazonSQS sqs,
    IOptions<GitHubWorkflowEventListenerOptions> config,
    IGitHubEventHandler eventHandler,
    ILogger<GitHubWorkflowEventListener> logger)
    : SqsListener(sqs, config.Value.QueueUrl, logger)
{
    protected override async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Received message from {queue}: {MessageId}", QueueUrl, message.MessageId);

        try
        {
            var eventType = TryParseMessageBody(message.Body);
            if (eventType != null)
                await eventHandler.Handle(eventType, message.Body, cancellationToken);
            else
                logger.LogInformation("Message from {queue}: {MessageId} was not readable", QueueUrl,
                    message.MessageId);
        }
        catch (Exception e)
        {
            logger.LogError("Failed to handle message: {id}, {err}", message.MessageId, e);
        }
    }

    private static GitHubWorkflowEventType? TryParseMessageBody(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<GitHubWorkflowEventType>(body);
        }
        catch (Exception)
        {
            return null;
        }
    }
}