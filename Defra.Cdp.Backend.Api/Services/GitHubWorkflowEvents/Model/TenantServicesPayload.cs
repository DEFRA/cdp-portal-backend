using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public class TenantServicesPayload
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }
    [JsonPropertyName("services")] public required List<Service> Services { get; init; }
}

public record Service
{
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("zone")] public required string Zone { get; init; }
    [JsonPropertyName("mongo")] public required bool Mongo { get; init; }
    [JsonPropertyName("redis")] public required bool Redis { get; init; }
    [JsonPropertyName("service_code")] public required string ServiceCode { get; init; }
    [JsonPropertyName("test_suite")] public string? TestSuite { get; init; }
    [JsonPropertyName("buckets")] public List<string>? Buckets { get; init; }
    [JsonPropertyName("queues")] public List<string>? Queues { get; init; }
    [JsonPropertyName("api_enabled")] public bool? ApiEnabled { get; init; }
    [JsonPropertyName("api_type")] public string? ApiType { get; init; }
}