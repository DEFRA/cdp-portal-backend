using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Actions.events;

public record ActionMessage
{
    [JsonPropertyName("action")] [JsonRequired] 
    public required string Action { get; init; }
    [JsonPropertyName("content")][JsonRequired] 
    public required JsonObject Content { get; init; }
}

public record AppConfigMessageContent
{
    [JsonPropertyName("commitSha")][JsonRequired]  public required string CommitSha { get; init; }
    [JsonPropertyName("commitTimestamp")][JsonRequired]  public required DateTime CommitTimestamp { get; init; }
    [JsonPropertyName("environment")][JsonRequired]  public required string Environment { get; init; }
}