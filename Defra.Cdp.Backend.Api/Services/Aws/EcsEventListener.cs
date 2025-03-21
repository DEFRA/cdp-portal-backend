using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Defra.Cdp.Backend.Api.Services.DeploymentTriggers;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public class EcsEventListener(
    IAmazonSQS sqs,
    IOptions<EcsEventListenerOptions> config,
    IEcsEventsService ecsEventsService,
    TaskStateChangeEventHandler taskStateChangeEventHandler,
    LambdaMessageHandler lambdaMessageHandler,
    DeploymentStateChangeEventHandler deploymentStateChangeEventHandler,
    DeploymentTriggerEventHandler deploymentTriggerEventHandler,
    ILogger<EcsEventListener> logger)
    : SqsListener(sqs, config.Value.QueueUrl, logger)
{
    private static DateTime GetTimeStamp(Message message, ILogger<EcsEventListener> logger)
    {
        if (message.Attributes.TryGetValue("SentTimestamp", out var sentTimestamp))
        {
            var milliseconds = Convert.ToDouble(sentTimestamp);
            return DateTime.UnixEpoch.AddMilliseconds(milliseconds);
        }
        
        logger.LogError("'Timestamp' attribute missing: {MessageMessageId}", message.MessageId);
        return DateTime.Now;
    }
    
    
    protected override async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        logger.LogDebug("Receive: {MessageMessageId}", message.MessageId);
        
        // keep a backup copy of the event (currently for debug/testing/replaying)
        var timestamp = GetTimeStamp(message, logger);
        await ecsEventsService.SaveMessage(message.MessageId, message.Body, timestamp, cancellationToken);
        await ProcessMessageAsync(message.MessageId, message.Body, cancellationToken);
    }
    
    private async Task ProcessMessageAsync(string id, string messageBody, CancellationToken cancellationToken)
    {
        var unknownEvent = JsonSerializer.Deserialize<EcsEventHeader>(messageBody);
        
        switch (unknownEvent?.DetailType)
        {
            case "ECS Task State Change":
                var ecsTaskEvent = JsonSerializer.Deserialize<EcsTaskStateChangeEvent>(messageBody);
                if (ecsTaskEvent == null)
                {
                    throw new Exception($"Unable to parse ECS Task State Change message {unknownEvent.Id}");
                }
                
                await taskStateChangeEventHandler.Handle(id, ecsTaskEvent, cancellationToken);
                break;
            case "ECS Lambda Deployment Updated":
            case "ECS Lambda Deployment Created":
            case "ECS Lambda Deployment Event":
                var ecsLambdaEvent = JsonSerializer.Deserialize<EcsDeploymentLambdaEvent>(messageBody);
                if (ecsLambdaEvent == null)
                {
                    throw new Exception($"Unable to parse Deployment Lambda message {unknownEvent.Id}");
                }
                
                await lambdaMessageHandler.Handle(id, ecsLambdaEvent, cancellationToken);
                break;
            case "ECS Deployment State Change":
                var ecsDeploymentEvent = JsonSerializer.Deserialize<EcsDeploymentStateChangeEvent>(messageBody);
                if (ecsDeploymentEvent == null)
                {
                    throw new Exception($"Unable to parse Deployment Lambda message {unknownEvent.Id}");
                }
                
                await deploymentStateChangeEventHandler.Handle(id, ecsDeploymentEvent, cancellationToken);
                await deploymentTriggerEventHandler.Handle(id, ecsDeploymentEvent, cancellationToken);
                break;
            default:
                logger.LogInformation("Not processing {Id}, no handler for {messageType}. message was {MessageBody}",
                    id, unknownEvent?.DetailType, messageBody);
                break;
        }
    }
    
    public async Task BackFill(CancellationToken cancellationToken)
    {
        logger.LogInformation("Starting back-fill");
        var cursor = await ecsEventsService.FindAll(cancellationToken);
        await cursor.ForEachAsync(async m =>
        {
            logger.LogInformation("Back-filling {MessageId}", m.MessageId);
            try
            {
                await ProcessMessageAsync(m.MessageId, m.Body, cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Back-fill for message {MessageId} failed", m.MessageId);
            }
        }, cancellationToken);
        logger.LogInformation("Finished back-fill");
    }
}