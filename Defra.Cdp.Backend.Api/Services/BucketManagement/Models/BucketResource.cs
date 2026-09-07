using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.BucketManagement.Models;

public record BucketObject
{
     [JsonPropertyName("name")]
     public String Name { get; init; } = "";

    [JsonPropertyName("size")]
    public Int128 Size { get; init; } = 0;

    [JsonPropertyName("isFolder")]
    public Boolean isFolder { get; init; } = false;
}