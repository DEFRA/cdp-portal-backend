using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Model;


public record EnabledApisPayload
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }
    [JsonPropertyName("apis")] public required List<EnabledApi> Apis { get; init; }
}

public record EnabledApi
{
    [JsonPropertyName("service")] public required string Service { get; init; }
    [JsonPropertyName("api")] public required string Api { get; init; }
}
