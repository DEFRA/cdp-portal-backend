using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Model;

public record AppConfigVersionPayload
{
    [JsonPropertyName("commitSha")] public required string CommitSha { get; init; }

    [JsonPropertyName("commitTimestamp")] public required DateTime CommitTimestamp { get; init; }

    [JsonPropertyName("environment")] public required string Environment { get; init; }
}