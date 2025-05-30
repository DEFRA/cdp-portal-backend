using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;


public record EnabledVanityUrlsPayload
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }
    [JsonPropertyName("urls")] public required List<EnabledVanityUrl> Urls { get; init; }
}

public record EnabledVanityUrl
{
    [JsonPropertyName("service")] public required string Service { get; init; }
    [JsonPropertyName("url")] public required string Url { get; init; }
}