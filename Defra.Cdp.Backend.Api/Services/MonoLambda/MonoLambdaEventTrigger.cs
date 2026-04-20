using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using Defra.Cdp.Backend.Api.Config;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.MonoLambda;

public abstract record MongoLambdaTriggerPayload {}

public class MonoLambdaTriggerEvent<T>
{
    [JsonPropertyName("event_type")]
    public required string EventType { get; init; }

    [JsonPropertyName("timestamp")]
    public required DateTime Timestamp { get; init; } = DateTime.UtcNow; 

    [JsonPropertyName("payload")]
    public required T Payload { get; init; }
}

public class MonoLambdaTrigger(IAmazonSimpleNotificationService sns, IOptions<MonoLambdaOptions> config, ILogger<MonoLambdaTrigger> logger)
{
    public async Task Trigger<T>(MonoLambdaTriggerEvent<T> trigger, string environment,
        CancellationToken cancellationToken)
    {

        if (config.Value.TopicArn == null)
        {
            logger.LogDebug("MonoLambda topic is not set, skipping trigger");
            return;
        }
        
        var message = JsonSerializer.Serialize(trigger);
        var hashBytes = MD5.HashData(Encoding.UTF8.GetBytes(message));
        var dedupeId = Convert.ToHexString(hashBytes);

        // config.Value.TopicArn
        var publishRequest = new PublishRequest {
            TopicArn = config.Value.TopicArn, 
            Message = message, 
            MessageGroupId = environment,
            MessageDeduplicationId = dedupeId,
            MessageAttributes = new Dictionary<string, MessageAttributeValue>
            {
                { "environment", new MessageAttributeValue { StringValue = environment, DataType = "String"} }
            }
        };

        var response = await sns.PublishAsync(publishRequest, cancellationToken);
        logger.LogInformation("Triggered MonoLambda event, environment:{Environment} of type {Type} dedupe: {dedupeId}, status: {Status}", environment, trigger.EventType, dedupeId, response?.HttpStatusCode);
    }
}