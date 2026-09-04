using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.BucketManagement.Models;

/**
 *  A BucketResourceUrl is an preSignedUrl for an S3 Object
 */
public record BucketResourceUrl
{
    [JsonPropertyName("method")]
    public string Method { get; init; } = "";

    [JsonPropertyName("url")]
    public string Url { get; init; } = "";
}