using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Model;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents;

/**
 * Listens for events sent by GitHub Workflows
 * Messages are sent by the workflows and contain event specific payloads
 */
public class GitHubWorkflowEventListener(
    IAmazonSQS sqs,
    IOptions<GitHubWorkflowEventListenerOptions> config,
    IGitHubWorkflowEventHandler eventHandler,
    ILogger<GitHubWorkflowEventListener> logger)
    : SqsListener(sqs, config.Value.QueueUrl, logger)
{
    protected override async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Received message from {QueueUrl}: {Id}", QueueUrl, message.MessageId);

        try
        {
            logger.LogDebug(message.Body);
            var eventType = TryParseMessageBody(message.Body);
            if (eventType != null)

                await eventHandler.Handle(eventType, message.Body, cancellationToken);
            else
                logger.LogInformation("Message from {QueueUrl}: {Id} was not readable: {Body}", QueueUrl,
                    message.MessageId, message.Body);
        }
        catch (Exception e)
        {
            logger.LogError("Failed to process message {Id} {Exception}", message.MessageId, e.Message);
        }
    }

    private static CommonEventWrapper? TryParseMessageBody(string body)
    {
        Console.WriteLine(body);
        try
        {
            return JsonSerializer.Deserialize<CommonEventWrapper>(body);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }
}