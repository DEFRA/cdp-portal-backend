using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public sealed record EcsDeploymentStateChange(

    [property: JsonPropertyName("detail-type")]
    string DetailType,
    [property: JsonPropertyName("account")]
    string Account,
    [property: JsonPropertyName("detail")]
    EcsDeploymentStateChangeDetail Detail
);

public sealed record EcsDeploymentStateChangeDetail(
    [property: JsonPropertyName("eventType")]
    string EventType,
    [property: JsonPropertyName("eventName")]
    string EventName,
    [property: JsonPropertyName("deploymentId")]
    string DeploymentId,
    [property: JsonPropertyName("updatedAt")]
    DateTime UpdatedAt,
    [property: JsonPropertyName("reason")]
    string Reason
);
