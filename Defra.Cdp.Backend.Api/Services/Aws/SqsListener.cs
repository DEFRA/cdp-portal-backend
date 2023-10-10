using Amazon.SQS;
using Amazon.SQS.Model;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public interface ISqsListener
{
    Task ReadAsync();
}

public abstract class SqsListener : ISqsListener, IDisposable
{
    protected readonly string QueueUrl;
    protected readonly IAmazonSQS Sqs;
    protected bool Enabled;
    protected int WaitTimeoutSeconds = 15;

    protected SqsListener(IAmazonSQS sqs, string queueUrl, bool enabled = true)
    {
        Sqs = sqs;
        QueueUrl = queueUrl;
        Enabled = enabled;
    }

    public void Dispose()
    {
        Enabled = false;
        Sqs.Dispose();
    }

    public async Task ReadAsync()
    {
        var receiveMessageRequest = new ReceiveMessageRequest
        {
            QueueUrl = QueueUrl,
            WaitTimeSeconds = WaitTimeoutSeconds
        };
        var falloff = 1;
        while (Enabled)
            try
            {
                var receiveMessageResponse = await Sqs.ReceiveMessageAsync(receiveMessageRequest);

                if (receiveMessageResponse.Messages.Count == 0) continue;

                foreach (var message in receiveMessageResponse.Messages)
                {
                    try
                    {
                        await HandleMessageAsync(message);
                    }
                    catch (Exception exception)
                    {
                        Console.Error.WriteLine(message.Body);
                        Console.Error.WriteLine(exception);
                        // TODO: support Dead Letter Queue 
                    }

                    var deleteRequest = new DeleteMessageRequest
                    {
                        QueueUrl = QueueUrl,
                        ReceiptHandle = message.ReceiptHandle
                    };

                    await Sqs.DeleteMessageAsync(deleteRequest);
                    falloff = 1;
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine(exception.Message);
                Thread.Sleep(1000 * Math.Min(60, falloff));
                falloff++;
                // TODO: decide how to handle failures here. what kind of failures are they? AWS connection stuff?
            }
    }

    public abstract Task HandleMessageAsync(Message message);
}