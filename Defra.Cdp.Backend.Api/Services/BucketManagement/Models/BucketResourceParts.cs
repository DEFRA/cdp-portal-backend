using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.BucketManagement.Models;

/**
 *  A BucketResourceParts is an preSignedUrl for an S3 Object
 */
public record BucketResourceParts
{
    [JsonPropertyName("parts")]
    public BucketResourcePart[] Parts { get; init; } = [];
}

public record BucketResourcePart
{
    [JsonPropertyName("partNumber")]
    public int PartNumber { get; init; } = 0;

    [JsonPropertyName("url")]
    public string Url { get; init; } = "";

    [JsonPropertyName("byteStartPosition")]
    public Int128 ByteStartPosition { get; init; } = 0;

    [JsonPropertyName("byteEndPosition")]
    public Int128 ByteEndPosition { get; init; } = 0;
}