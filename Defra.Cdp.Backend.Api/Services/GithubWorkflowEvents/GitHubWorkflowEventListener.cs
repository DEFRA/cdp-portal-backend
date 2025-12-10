using System.Diagnostics;
using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Aws;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents;


public interface IGithubWorkflowEventHandler
{
    public string EventType { get; }
    public Task Handle(string message, CancellationToken cancellation);
}

/**
 * Listens for events sent by GitHub Workflows
 * Messages are sent by the workflows and contain event specific payloads
 */
public class GithubWorkflowEventListener : SqsListener {
    
    private readonly Dictionary<string, IGithubWorkflowEventHandler> _handlers = new();
    private readonly ILogger<GithubWorkflowEventListener> _logger;
    
    public GithubWorkflowEventListener(
        IAmazonSQS sqs,
        IOptions<GithubWorkflowEventListenerOptions> config,
        IEnumerable<IGithubWorkflowEventHandler> eventHandlers,
        ILoggerFactory loggerFactory) : base(sqs, config.Value.QueueUrl, loggerFactory.CreateLogger<GithubWorkflowEventListener>())
    {
        _logger = loggerFactory.CreateLogger<GithubWorkflowEventListener>();
        
        // Injecting IEnumerable<IGithubWorkflowEventHandler> will provide all registered handlers.
        _logger.LogInformation("RegisteringIGithubWorkflowEventHandlers");
        foreach (var h in eventHandlers)
        {
            if (!_handlers.TryAdd(h.EventType, h))
            {
                _logger.LogError("Failed to register {NewHandler} for event {Event}, already registered to {Handler}",
                    h.GetType().FullName,
                    h.EventType,
                    _handlers[h.EventType].GetType().FullName
                );
                throw new ArgumentException($"Duplicate IGithubWorkflowEventHandlers registered for event type {h.EventType}");
            }
            _logger.LogInformation("Registered IGithubWorkflowEventHandlers handler for {EventType} to {HandlerName}", h.EventType, h.GetType().Name);
        }
        _logger.LogInformation("Registered {Count} IGithubWorkflowEventHandlers", _handlers.Count);
    }
    
    protected override async Task HandleMessageAsync(Message message, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Received message from {QueueUrl}: {Id}", QueueUrl, message.MessageId);

        try
        {
            await Handle(message, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogError("Failed to process message {Id} {Exception}", message.MessageId, e.Message);
        }
    }

    public async Task Handle(Message message, CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var eventWrapper = TryParseMessageBody(message.Body);
        _logger.LogInformation("Message from {QueueUrl}: {Id} took {ElapsedMilliseconds}ms to parse",
            QueueUrl, message.MessageId, sw.ElapsedMilliseconds);
        if (eventWrapper == null)
        {
            _logger.LogInformation("Message from {QueueUrl}: {Id} was not readable: {Body}", QueueUrl,
                message.MessageId, message.Body);
            return;
        }

        if (_handlers.TryGetValue(eventWrapper.EventType, out var handler))
        {
            await handler.Handle(message.Body, cancellationToken);
        }
        else
        {
            _logger.LogWarning("No handler found for event {EventType}", eventWrapper.EventType);
        }
        sw.Stop();
        _logger.LogInformation("Message from {QueueUrl}: {Id} took {ElapsedMilliseconds}ms to handle",
            QueueUrl, message.MessageId, sw.ElapsedMilliseconds);
    }

    private static CommonEventWrapper? TryParseMessageBody(string body)
    {
        try
        {
            return JsonSerializer.Deserialize<CommonEventWrapper>(body);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
            return null;
        }
    }
}