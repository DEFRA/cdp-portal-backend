using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Model;

public record SquidProxyConfigPayload
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }
    [JsonPropertyName("default_domains")] public required List<string> DefaultDomains { get; init; }
    [JsonPropertyName("services")] public required List<ServiceConfig> Services { get; init; }
}

public record ServiceConfig
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("allowed_domains")] public required List<string> AllowedDomains { get; init; }
}
