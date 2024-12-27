using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public record GitHubWorkflowEventWrapper
{
    [JsonPropertyName("eventType")] public required string EventType { get; init; }
}

public record Event<T> : GitHubWorkflowEventWrapper
{
    [JsonPropertyName("timestamp")] public required DateTime Timestamp { get; init; }
    [JsonPropertyName("payload")] public required T Payload { get; init; }
}
