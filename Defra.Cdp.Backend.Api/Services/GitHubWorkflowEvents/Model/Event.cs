using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public record GitHubWorkflowEventType
{
    private readonly string? _eventType;

    [JsonPropertyName("eventType")]
    public string? EventType
    {
        get => _eventType ?? Action;
        init => _eventType = value;
    }

    [JsonPropertyName("action")] public string? Action { get; init; }
}

public record Event<T> : GitHubWorkflowEventType
{
    private readonly T? _payload;
    [JsonPropertyName("timestamp")] public DateTime? Timestamp { get; init; }

    [JsonPropertyName("payload")]
    public T? Payload
    {
        get => _payload ?? Content;
        init => _payload = value;
    }

    [JsonPropertyName("content")] public T? Content { get; init; }
}

public record AppConfigVersionEvent : Event<AppConfigVersionPayload>;

public record VanityUrlsEvent : Event<VanityUrlsPayload>;