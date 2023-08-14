using System.Text.Json.Serialization;
using MongoDB.Bson;

namespace Defra.Cdp.Backend.Api.Models;

public sealed record EcsEvent(
    [property: JsonPropertyName("id")] string DeploymentId,
    string DetailType,
    string Account,
    [property: JsonPropertyName("time")] DateTime Timestamp,
    string Region,
    EcsEventDetail Detail
);

public sealed record EcsContainer(
    string Image,
    string ImageDigest,
    string Name,
    string LastStatus
);

public sealed record EcsEventDetail(
    DateTime CreatedAt,
    string Cpu,
    string Memory,
    string LastStatus,
    string DesiredStatus,
    List<EcsContainer> Containers,
    string TaskDefinitionArn
);

public sealed record EcsEventCopy(
    string MessageId, DateTime Timestamp, string Body)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; set; }
}