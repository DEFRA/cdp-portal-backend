using System.Text.Json.Serialization;
using MongoDB.Bson;

namespace Defra.Cdp.Backend.Api.Models;

public record DeployableArtifact
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; set; }

    public DateTime Created { get; set; } = DateTime.Now;

    public string Repo { get; set; } = default!;

    public string Tag { get; set; } = default!;

    public string Sha256 { get; set; } = default!;

    public string? GithubUrl { get; set; } = default!;

    public string? ServiceName { get; set; } = default!;

    public int ScannerVersion { get; set; } = default!;

    // TODO: replace this with references to the layers, maybe something like: {filename: layer}?  
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<DeployableArtifactFile> Files { get; set; } = new();

    public long? SemVer { get; set; }
}

public record DeployableArtifactFile(string FileName, string Path, string LayerSha256);

public record ServiceInfo(string ServiceName, string? GithubUrl, string ImageName);