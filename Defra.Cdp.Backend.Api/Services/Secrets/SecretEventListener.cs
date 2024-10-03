using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.Aws;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Secrets;

/**
 * Listens for events sent by the cdp-secret-manager-lambda.
 * Messages are sent cross-account to the portal and contain information about secret
 * creation/deletion/changes.
 */
public class SecretEventListener(
    IAmazonSQS sqs,
    IOptions<SecretEventListenerOptions> config,
    ISecretEventHandler secretEventHandler,
    ILogger<SecretEventListener> logger)
    : SqsListener(sqs, config.Value.QueueUrl, logger)
{
    protected override async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        logger.LogInformation("Received message from {queue}: {MessageId}", QueueUrl, message.MessageId);

        try
        {
            var secret = SecretEventHandler.TryParseMessageHeader(message.Body);
            if (secret != null)
            {
                await secretEventHandler.Handle(secret, cancellationToken);
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