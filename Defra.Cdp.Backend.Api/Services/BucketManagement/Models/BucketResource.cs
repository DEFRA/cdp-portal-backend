using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;

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
    public long Size { get; set; } = 0;

    [JsonPropertyName("modifiedDate")]
    public DateTime ModifiedDate { get; set; }

    [JsonPropertyName("createdDate")]
    public DateTime CreatedDate { get; set; }

    [JsonPropertyName("isFolder")]
    public bool IsFolder { get; init; } = false;

    [JsonPropertyName("user")]
    public UserDetails User { get; set; } = new UserDetails();
}