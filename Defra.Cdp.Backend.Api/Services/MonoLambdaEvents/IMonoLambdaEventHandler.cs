using System.Text.Json;

namespace Defra.Cdp.Backend.Api.Services.MonoLambdaEvents;

public interface IMonoLambdaEventHandler
{
    string EventType { get; }

    Task HandleAsync(JsonElement message, CancellationToken cancellationToken);
}