using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

[Obsolete("Use Entity")]
public record GrafanaDashboardPayload
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }

    [JsonPropertyName("entities")] public required List<string> Entities { get; init; }
}