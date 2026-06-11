using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Create.Models;

[Obsolete("replaced by tenant resource requests")]
public record CreateAdminResourceRequest
{
    [JsonPropertyName("service")]
    public required string Service { get; init; }

    [JsonPropertyName("bucketName")]
    public required string BucketName  { get; init; }

    [JsonPropertyName("environment")]
    public required string Environment  { get; init; }
}