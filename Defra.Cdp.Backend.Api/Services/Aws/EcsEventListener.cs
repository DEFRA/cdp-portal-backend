using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws.Deployments;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public class EcsEventListener : SqsListener
{
    private readonly IEcsEventsService _ecsEventsService;
    private readonly ILogger<EcsEventListener> _logger;
    
    private readonly TaskStateChangeEventHandler _taskStateChangeEventHandler;
    private readonly LambdaMessageHandlerV2 _lambdaMessageHandlerV2;
    private readonly DeploymentStateChangeEventHandler _deploymentStateChangeEventHandler;

    public EcsEventListener(IAmazonSQS sqs,
        IOptions<EcsEventListenerOptions> config,
        IEcsEventsService ecsEventsService,
        TaskStateChangeEventHandler taskStateChangeEventHandler,
        LambdaMessageHandlerV2 lambdaMessageHandlerV2,
        DeploymentStateChangeEventHandler deploymentStateChangeEventHandler,
        ILogger<EcsEventListener> logger) : base(sqs, config.Value.QueueUrl, logger)
    {
        _logger = logger;
        _ecsEventsService = ecsEventsService;
        _taskStateChangeEventHandler = taskStateChangeEventHandler;
        _lambdaMessageHandlerV2 = lambdaMessageHandlerV2;
        _deploymentStateChangeEventHandler = deploymentStateChangeEventHandler;
    }
    
    
    public override async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        _logger.LogDebug("Receive: {MessageMessageId}", message.MessageId);
    
        // keep a backup copy of the event (currently for debug/testing/replaying)
        await _ecsEventsService.SaveMessage(message.MessageId, message.Body, cancellationToken);
        await ProcessMessageAsync(message.MessageId, message.Body, cancellationToken);
    }
    
    private async Task ProcessMessageAsync(string id, string messageBody, CancellationToken cancellationToken)
    {
        var unknownEvent = JsonSerializer.Deserialize<UnknownEventType>(messageBody);

        switch (unknownEvent?.DetailType)
        {
            case "ECS Task State Change":
                var ecsTaskEvent = JsonSerializer.Deserialize<EcsTaskStateChangeEvent>(messageBody);
                if (ecsTaskEvent == null)
                {
                    throw new Exception($"Unable to parse ECS Task State Change message {unknownEvent.Id}");
                }
                await _taskStateChangeEventHandler.Handle(id, ecsTaskEvent, cancellationToken);
                break;
            case "ECS Lambda Deployment Updated":
            case "ECS Lambda Deployment Created":
                var ecsLambdaEvent = JsonSerializer.Deserialize<EcsTaskStateChangeEvent>(messageBody);
                if (ecsLambdaEvent == null)
                {
                    throw new Exception($"Unable to parse Deployment Lambda message {unknownEvent.Id}");
                }
                await _lambdaMessageHandlerV2.Handle(id, ecsLambdaEvent, cancellationToken);
                break;
            case "ECS Deployment State Change":
                var ecsDeploymentEvent = JsonSerializer.Deserialize<EcsDeploymentStateChange>(messageBody);
                if (ecsDeploymentEvent == null)
                {
                    throw new Exception($"Unable to parse Deployment Lambda message {unknownEvent.Id}");
                }
                await _deploymentStateChangeEventHandler.Handle(id, ecsDeploymentEvent, cancellationToken);
                break;
            default:
                _logger.LogInformation("Not processing {Id}, details was null. message was {MessageBody}", id, messageBody);
                break;
        }

    }

    public async Task BackFill(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting back-fill");
        var cursor = await _ecsEventsService.FindAll(cancellationToken);
        await cursor.ForEachAsync(async m =>
        {
            _logger.LogInformation("Back-filling {MessageId}", m.MessageId);
            try
            {
                await ProcessMessageAsync(m.MessageId, m.Body, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Back-fill for message {MessageId} failed", m.MessageId);
            }
        }, cancellationToken);
        _logger.LogInformation("Finished back-fill");
    }
}