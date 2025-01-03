using Amazon.SQS;
using Amazon.SQS.Model;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public interface ISqsListener
{
    public Task ReadAsync(CancellationToken cancellationToken);
}

public abstract class SqsListener(IAmazonSQS sqs, string queueUrl, ILogger logger, bool enabled = true)
    : ISqsListener, IDisposable
{
    protected readonly string QueueUrl = queueUrl;
    private const int WaitTimeoutSeconds = 15;
    
    public void Dispose()
    {
        enabled = false;
        sqs.Dispose();
    }

    public async Task ReadAsync(CancellationToken cancellationToken)
    {
#pragma warning disable CS0618 // Type or member is obsolete        
        var receiveMessageRequest = new ReceiveMessageRequest
        {
            // Replacing AttributeNames with MessageSystemAttributeNames causes attributes to not return - might just be Localstack
            QueueUrl = QueueUrl, WaitTimeSeconds = WaitTimeoutSeconds, AttributeNames = ["All"]
        };
#pragma warning restore CS0618 // Type or member is obsolete        
        logger.LogInformation("Listening for events on {queue}", QueueUrl);
        
        var falloff = 1;
        while (enabled)
            try
            {
                var receiveMessageResponse = await sqs.ReceiveMessageAsync(receiveMessageRequest, cancellationToken);

                if (receiveMessageResponse.Messages.Count == 0) continue;

                foreach (var message in receiveMessageResponse.Messages)
                {
                    try
                    {
                        await HandleMessageAsync(message, cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(message.Body);
                        logger.LogError(exception.Message);
                    }

                    var deleteRequest = new DeleteMessageRequest
                    {
                        QueueUrl = QueueUrl, ReceiptHandle = message.ReceiptHandle
                    };

                    await sqs.DeleteMessageAsync(deleteRequest, cancellationToken);
                    falloff = 1;
                }
            }
            catch (Exception exception)
            {
                logger.LogError(exception.Message);
                await Task.Delay(1000 * Math.Min(60, falloff), cancellationToken);
                falloff++;
                // TODO: decide how to handle failures here. what kind of failures are they? AWS connection stuff?
            }
    }
    
    protected abstract Task HandleMessageAsync(Message message, CancellationToken cancellationToken);
}