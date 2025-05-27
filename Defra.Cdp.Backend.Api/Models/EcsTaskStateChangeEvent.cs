using System.Text.Json.Serialization;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson.Serialization.IdGenerators;

namespace Defra.Cdp.Backend.Api.Models;

public sealed record EcsTaskStateChangeEvent(
    [property: JsonPropertyName("id")] string DeploymentId,
    [property: JsonPropertyName("detail-type")]
    string DetailType,
    [property: JsonPropertyName("account")]
    string Account,
    [property: JsonPropertyName("time")] DateTime Timestamp,
    [property: JsonPropertyName("region")] string Region,
    [property: JsonPropertyName("detail")] EcsEventDetail Detail,
    [property: JsonPropertyName("deployed_by")]
    string? DeployedBy,
    [property: JsonPropertyName("cdp_deployment_id")]
    string? CdpDeploymentId
);

public sealed record EcsContainer(
    [property: JsonPropertyName("image")] string Image,
    [property: JsonPropertyName("imageDigest")]
    string ImageDigest,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lastStatus")]
    string LastStatus,
    [property: JsonPropertyName("desiredStatus")]
    string DesiredStatus,
    [property: JsonPropertyName("exitCode")]
    int? ExitCode = null,
    [property: JsonPropertyName("reason")]
    string? Reason = null
);

public sealed record EcsEventDetail(
    [property: JsonPropertyName("createdAt")]
    DateTime CreatedAt,
    [property: JsonPropertyName("cpu")] string Cpu,
    [property: JsonPropertyName("memory")] string Memory,
    [property: JsonPropertyName("lastStatus")]
    string LastStatus,
    [property: JsonPropertyName("desiredStatus")]
    string DesiredStatus,
    [property: JsonPropertyName("containers")]
    List<EcsContainer> Containers,
    [property: JsonPropertyName("taskDefinitionArn")]
    string TaskDefinitionArn,
    [property: JsonPropertyName("taskArn")]
    string TaskArn,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("startedBy")]
    string StartedBy,
    [property: JsonPropertyName("deploymentId")]
    string? EcsSvcDeploymentId,
    [property: JsonPropertyName("stopCode")]
    string? StopCode = default,
    [property: JsonPropertyName("stoppedReason")]
    string? StoppedReason = default
);

public sealed record EcsEventCopy(
    string MessageId,
    DateTime Timestamp,
    string Body)
{
    [BsonId(IdGenerator = typeof(ObjectIdGenerator))]
    [JsonIgnore(Condition = JsonIgnoreCondition.Always)]
    public ObjectId? Id { get; init; } = default!;
}