using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public record VanityUrlsPayload
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }
    [JsonPropertyName("services")] public required List<ServiceVanityUrls> Services { get; init; }
}

public record ServiceVanityUrls
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("urls")] public required List<VanityUrl> Urls { get; init; }
}

public record VanityUrl
{
    [JsonPropertyName("host")] public required string Host { get; init; }
    [JsonPropertyName("domain")] public required string Domain { get; init; }
}