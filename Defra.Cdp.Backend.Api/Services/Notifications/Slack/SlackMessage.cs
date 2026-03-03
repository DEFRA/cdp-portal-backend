using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;

namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack;

/// <summary>
/// Payload used by https://github.com/DEFRA/cdp-notification-lambda/
/// </summary>
public class SlackMessagePayload
{
    
    public class SlackMessage
    {
        [property: JsonPropertyName("channel")] public required string Channel { get; init; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("text")] public string? Text { get; init; }
        
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        [property: JsonPropertyName("blocks")] public List<Block>? Blocks { get; init; }
    }

    [property: JsonPropertyName("team")] public required string Team { get; init; }
    [property: JsonPropertyName("message")] public required SlackMessage Message { get; init; }
}
