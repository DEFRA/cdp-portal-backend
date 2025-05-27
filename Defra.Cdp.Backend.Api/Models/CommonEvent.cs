using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public record CommonEventWrapper
{
    [JsonPropertyName("eventType")] public required string EventType { get; init; }
}

public record CommonEvent<T> : CommonEventWrapper
{
    [JsonPropertyName("timestamp")] public required DateTime Timestamp { get; init; }
    [JsonPropertyName("payload")] public required T Payload { get; init; }
}