using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.Aws;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.MonoLambdaEvents;

/// <summary>
/// Generic handler for events sent to portal from the cdp-mono-lambda.
/// Requires one or more IMonoLambdaEventHandlers to be registered.
/// </summary>
public class MonoLambdaEventListener : SqsListener
{

    private readonly Dictionary<string, IMonoLambdaEventHandler> _handlers = new();

    private readonly ILogger<MonoLambdaEventListener> _logger;
    
    
    public MonoLambdaEventListener(IAmazonSQS sqs, IOptions<LambdaEventListenerOptions> config, IEnumerable<IMonoLambdaEventHandler> eventHandlers, ILogger<MonoLambdaEventListener> logger) 
        : base(sqs, config.Value.QueueUrl, logger, config.Value.Enabled)
    {
        _logger = logger;
        
        // Injecting IEnumerable<IMonoLambdaEventHandler> will provide all registered handlers.
        logger.LogInformation("Registering lambda event handlers");
        foreach (var h in eventHandlers)
        {
            _handlers[h.EventType] = h;
            logger.LogInformation("Registered event handler for {EventType} to {HandlerName}", h.EventType, h.GetType().Name);
        }
    }
    
    protected override async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing a new lambda message {Id}", message.MessageId);
        using var document = JsonDocument.Parse(message.Body);
        
        var root = document.RootElement.Clone();
        var eventType = root.GetProperty("event_type").GetString();

        if (eventType == null)
        {
            throw new Exception("EventType is not set");
        }
        
        // pass to handler
        if (_handlers.TryGetValue(eventType, out var handler))
        {
            _logger.LogInformation("Processing event {EventType} with handler {Name}", eventType, handler.GetType().Name);
            await handler.HandleAsync(root, cancellationToken);
        }
        else
        {
            // TODO: maybe register a default handler?
            _logger.LogInformation("No handler found for event {EventType}", eventType);
        }
    }
}
