using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.BucketManagement.Models;

public record BucketResourceTreeNode
{
    [JsonPropertyName("path")]
    public string Path { get; init; } = "";

    [JsonPropertyName("isCurrent")]
    public bool IsCurrent { get; init; } = false;

    [JsonPropertyName("subNodes")]
    public OrderedDictionary<string, BucketResourceTreeNode> subNodes { get; init; } = [];
}