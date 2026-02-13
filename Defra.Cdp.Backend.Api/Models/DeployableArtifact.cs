using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Models;

[BsonIgnoreExtraElements]
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

    public string? ServiceName { get; init; } = default!;

    public int ScannerVersion { get; init; } = default!;

    public long? SemVer { get; init; }

    public static DeployableArtifact FromEcrEvent(SqsEcrEvent ecrEvent)
    {
        var semver =  Defra.Cdp.Backend.Api.Utils.SemVer.SemVerAsLong(ecrEvent.Detail.ImageTag);

        return new DeployableArtifact
        {
            ScannerVersion = 1,
            ServiceName = ecrEvent.Detail.RepositoryName,
            Repo = ecrEvent.Detail.RepositoryName,
            Tag = ecrEvent.Detail.ImageTag,
            SemVer = semver,
            Sha256 = ecrEvent.Detail.ImageDigest,
        };
    }
}

public sealed record ArtifactVersion(string Name, string Version);

public sealed record TagInfo(string Tag, DateTime Created);