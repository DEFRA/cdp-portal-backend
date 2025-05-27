using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public record AppConfigPayload
{
    [JsonPropertyName("commitSha")] public required string CommitSha { get; init; }

    [JsonPropertyName("commitTimestamp")] public required DateTime CommitTimestamp { get; init; }

    [JsonPropertyName("environment")] public required string Environment { get; init; }

    [JsonPropertyName("entities")] public required List<string> Entities { get; init; }
}