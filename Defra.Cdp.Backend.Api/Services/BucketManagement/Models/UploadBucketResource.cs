using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.BucketManagement.Models;


public class UploadBucketResource
{
    [JsonPropertyName("size")]
    public Int128 Size { get; set; } = 0;
}