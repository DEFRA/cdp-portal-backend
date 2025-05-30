using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;
using MongoDB.Bson.Serialization.Options;

namespace Defra.Cdp.Backend.Api.Models;

public sealed class DeployableArtifact
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [BsonIgnoreIfDefault]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;

    public DateTime Created { get; init; } = DateTime.Now;

    public string Repo { get; init; } = default!;

    public string Tag { get; init; } = default!;

    public string Sha256 { get; init; } = default!;

    public string? GithubUrl { get; init; } = default!;

    public string? ServiceName { get; init; } = default!;

    public int ScannerVersion { get; init; } = default!;

    public IEnumerable<RepositoryTeam> Teams { get; init; } = default!;

    // TODO: replace this with references to the layers, maybe something like: {filename: layer}?  
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public List<DeployableArtifactFile> Files { get; init; } = [];

    public long? SemVer { get; init; }

    // Is it a microservice or a test job
    public string? RunMode { get; init; } = default;

    [BsonDictionaryOptions(DictionaryRepresentation.Document)]
    public Dictionary<string, string> Annotations { get; init; } = [];
}

public sealed record DeployableArtifactFile(string FileName, string Path, string LayerSha256);

public sealed record ServiceInfo(
    string ServiceName,
    string? GithubUrl,
    string ImageName,
    IEnumerable<RepositoryTeam> Teams);

public sealed record TagInfo(string Tag, DateTime Created);