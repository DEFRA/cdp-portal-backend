using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.BucketManagement.Models;


public class RenameBucketResource
{
    [JsonPropertyName("newName")]
    public string NewName { get; set; } = "";
}