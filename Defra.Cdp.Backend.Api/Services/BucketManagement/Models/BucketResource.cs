using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.BucketManagement.Models;

/**
 *  A BucketResource is an S3 Object treated like a File or Folder in a filesystem
 */
public record BucketResource
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    [JsonPropertyName("size")]
    public long Size { get; init; } = 0;

    [JsonPropertyName("lastModified")]
    public DateTime LastModified { get; init; }

    [JsonPropertyName("isFolder")]
    public bool IsFolder { get; init; } = false;
}