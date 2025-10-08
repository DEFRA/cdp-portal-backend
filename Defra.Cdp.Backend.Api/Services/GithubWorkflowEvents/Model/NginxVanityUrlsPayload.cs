using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

[Obsolete("Use Tenant")]
public record NginxVanityUrlsPayload
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }
    [JsonPropertyName("services")] public required List<NginxVanityUrls> Services { get; init; }
}

[Obsolete("Use Tenant")]
public record NginxVanityUrls
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("urls")] public required List<NginxVanityUrl> Urls { get; init; }
}

[Obsolete("Use Tenant")]
public record NginxVanityUrl
{
    [JsonPropertyName("host")] public required string Host { get; init; }
    [JsonPropertyName("domain")] public required string Domain { get; init; }
}