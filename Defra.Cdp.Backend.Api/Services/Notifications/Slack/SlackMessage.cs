using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack;

/// <summary>
/// Payload used by https://github.com/DEFRA/cdp-notification-lambda/
/// </summary>
public class SlackMessagePayload
{
    public class SlackMessage
    {
        [property: JsonPropertyName("channels")] public required string Channel { get; init; }
        [property: JsonPropertyName("text")] public string? Text { get; init; }
        [property: JsonPropertyName("blocks")] public string? Blocks { get; init; }
    }

    [property: JsonPropertyName("team")] public required string Team { get; init; }
    [property: JsonPropertyName("message")] public required SlackMessage Message { get; init; }
}