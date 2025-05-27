using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public class TenantBucketsPayload
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }
    [JsonPropertyName("buckets")] public required List<Bucket> Buckets { get; init; }
}

public class Bucket
{
    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("exists")] public required bool Exists { get; init; }

    [JsonPropertyName("services_with_access")] public required List<string> ServicesWithAccess { get; init; }

}