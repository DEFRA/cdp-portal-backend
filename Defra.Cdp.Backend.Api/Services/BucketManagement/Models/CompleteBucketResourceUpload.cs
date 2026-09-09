using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.BucketManagement.Models;

public record CompleteBucketResourceUpload
{
    [JsonPropertyName("uploadId")]
    public string UploadId { get; init; } = "";

    [JsonPropertyName("parts")]
    public CompleteBucketResourceUploadPart[] Parts { get; init; } = [];
}

public record CompleteBucketResourceUploadPart
{
    [JsonPropertyName("partNumber")]
    public int PartNumber { get; init; } = 0;

    [JsonPropertyName("eTag")]
    public string ETag { get; init; } = "";
}