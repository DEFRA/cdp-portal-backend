using System.Diagnostics;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents;

/**
 * Listens for events sent by Github Workflows
 * Messages are sent by the workflows and contain event specific payloads
 */
public class GithubWorkflowEventListener(
    IAmazonSQS sqs,
    IOptions<GithubWorkflowEventListenerOptions> config,
    IGithubWorkflowEventHandler eventHandler,
    ILogger<GithubWorkflowEventListener> logger)
    : SqsListener(sqs, config.Value.QueueUrl, logger)
{
    protected override async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Received message from {QueueUrl}: {Id}", QueueUrl, message.MessageId);

        try
        {
            await Handle(message, cancellationToken);
        }
        catch (Exception e)
        {
            logger.LogError("Failed to process message {Id} {Exception}", message.MessageId, e.Message);
        }
    }

    public async Task Handle(Message message, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var eventWrapper = TryParseMessageBody(message.Body);
        logger.LogInformation("Message from {QueueUrl}: {Id} took {ElapsedMilliseconds}ms to parse",
            QueueUrl, message.MessageId, sw.ElapsedMilliseconds);
        if (eventWrapper != null)
        {
            await eventHandler.Handle(eventWrapper, message.Body, cancellationToken);
            sw.Stop();
            logger.LogInformation("Message from {QueueUrl}: {Id} took {ElapsedMilliseconds}ms to handle",
                QueueUrl, message.MessageId, sw.ElapsedMilliseconds);
        }
        else
            logger.LogInformation("Message from {QueueUrl}: {Id} was not readable: {Body}", QueueUrl,
                message.MessageId, message.Body);
    }

    private static CommonEventWrapper? TryParseMessageBody(string body)
    {
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