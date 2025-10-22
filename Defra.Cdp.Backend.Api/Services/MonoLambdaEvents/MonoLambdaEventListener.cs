using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Services.Aws;
using Defra.Cdp.Backend.Api.Services.EventHistory;
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
    private readonly EventHistoryRepository<MonoLambdaEventListener> _eventHistory;


    public MonoLambdaEventListener(
        IAmazonSQS sqs,
        IEventHistoryFactory eventHistoryFactory,
        IOptions<LambdaEventListenerOptions> config,
        IEnumerable<IMonoLambdaEventHandler> eventHandlers,
        ILogger<MonoLambdaEventListener> logger) : base(sqs, config.Value.QueueUrl, logger, config.Value.Enabled)
    {
        _logger = logger;
        _eventHistory = eventHistoryFactory.Create<MonoLambdaEventListener>();

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
        await Handle(message, cancellationToken);
    }

    public async Task Handle(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Processing a new lambda message {Id}", message.MessageId);
        using var document = JsonDocument.Parse(message.Body);

        var root = document.RootElement.Clone();

        if (!root.TryGetProperty("event_type", out var eventTypeElement))
        {
            throw new InvalidOperationException("Payload missing event_type property.");
        }

        if (eventTypeElement.ValueKind != JsonValueKind.String)
        {
            throw new InvalidOperationException("event_type must be a JSON string.");
        }

        var eventType = eventTypeElement.GetString();
        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new InvalidOperationException("event_type must be a non-empty string.");
        }

        if (_handlers.TryGetValue(eventType, out var handler))
        {
            if (handler.PersistEvents)
            {
                await _eventHistory.PersistEvent(message.MessageId, root, cancellationToken);
            }

            _logger.LogInformation("Processing event {EventType} with handler {Name}", eventType, handler.GetType().Name);
            await handler.HandleAsync(root, cancellationToken);
        }
        else
        {
            _logger.LogInformation("No handler found for event {EventType}", eventType);
        }
    }
}