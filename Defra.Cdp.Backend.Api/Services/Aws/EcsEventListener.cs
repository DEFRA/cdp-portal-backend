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
    private readonly DeploymentEventHandler _deploymentEventHandler;
    private readonly LambdaMessageHandler _lambdaMessageHandler;
    
    public EcsEventListener(IAmazonSQS sqs,
        IOptions<EcsEventListenerOptions> config,
        IEcsEventsService ecsEventsService,
        DeploymentEventHandler deploymentEventHandler,
        LambdaMessageHandler lambdaMessageHandler,
        ILogger<EcsEventListener> logger) : base(sqs, config.Value.QueueUrl, logger)
    {
        _logger = logger;
        _ecsEventsService = ecsEventsService;
        _deploymentEventHandler = deploymentEventHandler;
        _lambdaMessageHandler = lambdaMessageHandler;
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
        var ecsEvent = JsonSerializer.Deserialize<EcsEvent>(messageBody);

        switch (ecsEvent?.DetailType)
        {
            case "ECS Task State Change":
                await _deploymentEventHandler.Handle(id, ecsEvent, cancellationToken);
                break;
            case "ECS Lambda Deployment Updated":
            case "ECS Lambda Deployment Created":
                await _lambdaMessageHandler.Handle(id, ecsEvent, cancellationToken);
                break;
            default:
                if (ecsEvent?.Detail == null)
                    _logger.LogInformation("Not processing {Id}, details was null. message was {MessageBody}", id, messageBody);
                else
                    _logger.LogInformation("Not processing {Id}, detail type was {DetailType}", id, ecsEvent.DetailType);
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