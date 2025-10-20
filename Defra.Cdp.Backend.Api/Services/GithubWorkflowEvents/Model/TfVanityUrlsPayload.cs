using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;


[Obsolete("Use Entity")]
public record TfVanityUrlsPayload
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }
    [JsonPropertyName("vanity_urls")] public required List<TfVanityUrl> VanityUrls { get; init; }
}


[Obsolete("Use Entity")]
public record TfVanityUrl
{
    [JsonPropertyName("service_name")] public string ServiceName { get; init; }
    [JsonPropertyName("public_url")] public required string PublicUrl { get; init; }
    [JsonPropertyName("enable_alb")] public required bool EnableAlb { get; init; }
    [JsonPropertyName("enable_acm")] public required bool EnableAcm { get; init; }
    [JsonPropertyName("is_api")] public bool IsApi { get; init; }
}