using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Models;

public sealed class Deployment
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    // default condition is always ignore but it's good ot be explicit
    public ObjectId? Id { get; init; } = default!;

    public string DeploymentId { get; init; } = default!;
    public string Environment { get; init; } = default!;
    public string Service { get; init; } = default!;
    public string Version { get; init; } = default!;

    public string? User { get; init; }
    public DateTime DeployedAt { get; init; } = default!;
    public string Status { get; init; } = default!;
    public string DockerImage { get; init; } = default!;
    public string? TaskId { get; init; }
}