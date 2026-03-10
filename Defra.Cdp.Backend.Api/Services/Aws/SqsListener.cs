using Amazon.SQS;
using Amazon.SQS.Model;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public abstract class SqsListener(IAmazonSQS sqs, string queueUrl, ILogger logger, bool enabled = true)
    : BackgroundService {
    
    protected readonly string QueueUrl = queueUrl;
    private bool _enabled = enabled;
    private const int WaitTimeoutSeconds = 15;
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_enabled)
        {
            logger.LogInformation("Listener for {QueueUrl} is disabled", QueueUrl);
            return;
        }

#pragma warning disable CS0618 // Type or member is obsolete        
        var receiveMessageRequest = new ReceiveMessageRequest
        {
            // Replacing AttributeNames with MessageSystemAttributeNames causes attributes to not return - might just be Localstack
            QueueUrl = QueueUrl,
            WaitTimeSeconds = WaitTimeoutSeconds,
            AttributeNames = ["All"]
        };
#pragma warning restore CS0618 // Type or member is obsolete

        var deleteRequest = new DeleteMessageRequest
        {
            QueueUrl = QueueUrl
        };

        logger.LogInformation("Listening for events on {Queue}", QueueUrl);

        var falloff = 1;
        ReceiveMessageResponse receiveMessageResponse;

        while (!stoppingToken.IsCancellationRequested)
            try
            {
                receiveMessageResponse = await sqs.ReceiveMessageAsync(receiveMessageRequest, stoppingToken);
                if (receiveMessageResponse.Messages == null) continue;
                if (receiveMessageResponse.Messages.Count == 0) continue;

                foreach (var message in receiveMessageResponse.Messages)
                {
                    // Ensure we don't cancel the handler mid-request
                    if (stoppingToken.IsCancellationRequested) break;
                    var innerCancellationToken = CancellationToken.None;

                    try
                    {
                        await HandleMessageAsync(message, innerCancellationToken);
                    }
                    catch (Exception exception)
                    {

                        logger.LogError(exception, "Failed to process message");
                        logger.LogError("Message: {Id} - Exception: {Message}", message.MessageId, exception.Message);
                    }

                    deleteRequest.ReceiptHandle = message.ReceiptHandle;

                    await sqs.DeleteMessageAsync(deleteRequest, innerCancellationToken);
                    falloff = 1;
                }
            }
            catch (Exception exception)
            {
                logger.LogError("{Message}", exception.Message);
                await Task.Delay(1000 * Math.Min(60, falloff), stoppingToken);
                falloff++;
            }
    }


    public override Task StopAsync(CancellationToken cancellationToken)
    {
        logger.LogInformation("Stopping event listener on {Queue}", QueueUrl);
        return base.StopAsync(cancellationToken);
    }

    protected abstract Task HandleMessageAsync(Message message, CancellationToken cancellationToken);
}