namespace Defra.Cdp.Backend.Api.Models;

public sealed record ManifestImageConfig(
    Dictionary<string, string> Labels,
    string Image
);

public sealed record ManifestImage(ManifestImageConfig config, string created, string architecture);

public sealed record Blob(string mediaType, int size, string digest);

public sealed record Manifest
{
    public string name { get; init; } 
    public string tag { get; init; }
    public Blob config { get; init; }
    public List<Blob> layers { get; init; }
    public string? digest { get; set; }
}

public sealed record Catalog(List<string> repositories);

public sealed record ImageTagList(string name, List<string> tags);