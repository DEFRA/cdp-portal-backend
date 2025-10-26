using System.Text.Json;

namespace Defra.Cdp.Backend.Api.Services.MonoLambdaEvents;

public interface IMonoLambdaEventHandler
{
    /// <summary>
    /// Handler will receive all messages where `event_type` matches this value
    /// </summary>
    string EventType { get; }
    
    /// <summary>
    /// When true, copies of the events handled will be stored in the EventHistoryService
    /// </summary>
    bool PersistEvents { get; }
    
    /// <summary>
    /// Message handler implementation.
    /// </summary>
    /// <param name="message"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    Task HandleAsync(JsonElement message, CancellationToken cancellationToken);
}