using Amazon.SQS;
using Amazon.SQS.Model;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public interface ISqsListener
{
    Task ReadAsync(CancellationToken cancellationToken);
}

public abstract class SqsListener : ISqsListener, IDisposable
{
    protected readonly string QueueUrl;
    protected readonly IAmazonSQS Sqs;
    protected bool Enabled;
    protected int WaitTimeoutSeconds = 15;

    private ILogger Logger;
    
    protected SqsListener(IAmazonSQS sqs, string queueUrl, ILogger logger, bool enabled = true)
    {
        Sqs = sqs;
        QueueUrl = queueUrl;
        Enabled = enabled;
        Logger = logger;
    }

    public void Dispose()
    {
        Enabled = false;
        Sqs.Dispose();
    }

    public async Task ReadAsync(CancellationToken cancellationToken)
    {
        var receiveMessageRequest = new ReceiveMessageRequest
        {
            QueueUrl = QueueUrl, WaitTimeSeconds = WaitTimeoutSeconds
        };
        
        Logger.LogInformation("Listening for events on {queue}", QueueUrl);
        
        var falloff = 1;
        while (Enabled)
            try
            {
                var receiveMessageResponse = await Sqs.ReceiveMessageAsync(receiveMessageRequest, cancellationToken);

                if (receiveMessageResponse.Messages.Count == 0) continue;

                foreach (var message in receiveMessageResponse.Messages)
                {
                    try
                    {
                        await HandleMessageAsync(message, cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        Logger.LogError(message.Body);
                        Logger.LogError(exception.Message);
                        // TODO: support Dead Letter Queue 
                    }

                    var deleteRequest = new DeleteMessageRequest
                    {
                        QueueUrl = QueueUrl, ReceiptHandle = message.ReceiptHandle
                    };

                    await Sqs.DeleteMessageAsync(deleteRequest, cancellationToken);
                    falloff = 1;
                }
            }
            catch (Exception exception)
            {
                Logger.LogError(exception.Message);
                await Task.Delay(1000 * Math.Min(60, falloff), cancellationToken);
                falloff++;
                // TODO: decide how to handle failures here. what kind of failures are they? AWS connection stuff?
            }
    }

    public abstract Task HandleMessageAsync(Message message, CancellationToken cancellationToken);
}