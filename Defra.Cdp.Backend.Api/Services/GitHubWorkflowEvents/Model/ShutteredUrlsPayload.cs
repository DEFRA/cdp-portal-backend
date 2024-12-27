using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public class ShutteredUrlsPayload
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }

    [JsonPropertyName("urls")] public required List<string> Urls { get; init; }
}