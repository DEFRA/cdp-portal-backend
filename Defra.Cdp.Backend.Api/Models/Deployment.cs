using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Models;

public sealed record Deployment(
    [property: BsonIgnoreIfDefault]
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Always)] // default condition is always ignore but it's good ot be explicit
    string? Id,
    string DeploymentId,
    string Environment,
    string Service,
    string Version,
    string? User,
    DateTime DeployedAt,
    string Status,
    string DockerImage,
    string? TaskId
);