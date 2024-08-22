using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public sealed record ManifestImageConfig(
    Dictionary<string, string> Labels,
    string Image
);

public sealed record ManifestImage(ManifestImageConfig config, string created, string architecture);

public sealed record Blob(string mediaType, int size, string digest);

public sealed record Manifest
{
    [property: JsonPropertyName("name")]
    public string? name { get; init; }

    [property: JsonPropertyName("tag")]
    public string? tag { get; init; }
    
    [property: JsonPropertyName("config")]
    public Blob? config { get; init; }
    
    [property: JsonPropertyName("layers")]
    public List<Blob> layers { get; init; } = [];
    
    [property: JsonPropertyName("digest")]
    public string? digest { get; set; }
}

public sealed record Catalog(List<string> repositories);

public sealed record ImageTagList(string name, List<string> tags);