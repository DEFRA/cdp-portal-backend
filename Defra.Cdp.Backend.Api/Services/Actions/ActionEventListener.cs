using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.Aws;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Actions;

/**
 * Listens for events sent by Github actions
 * Messages are sent by the actions and contain action specific information 
 */
public class ActionEventListener(
    IAmazonSQS sqs,
    IOptions<ActionEventListenerOptions> config,
    IActionEventHandler actionEventHandler,
    ILogger<ActionEventListener> logger)
    : SqsListener(sqs, config.Value.QueueUrl, logger)
{
    protected override async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Received message from {queue}: {MessageId}", QueueUrl, message.MessageId);

        try
        {
            var actionMessage = ActionEventHandler.TryParseMessageBody(message.Body);
            if (actionMessage != null)
            {
                await actionEventHandler.Handle(actionMessage, cancellationToken);
            }
            else
            {
                logger.LogInformation("Message from {queue}: {MessageId} was not readable", QueueUrl,
                    message.MessageId);
            }
        }
        catch (Exception e)
        {
            logger.LogError("Failed to handle message: {id}, {err}", message.MessageId, e);
        }
    }
}