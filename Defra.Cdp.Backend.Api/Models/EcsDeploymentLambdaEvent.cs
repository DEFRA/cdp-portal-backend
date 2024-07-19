using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public sealed record EcsDeploymentLambdaEvent(

    [property: JsonPropertyName("detail-type")]
    string DetailType,
    [property: JsonPropertyName("account")]
    string Account,
    [property: JsonPropertyName("detail")]
    EcsDeploymentLambdaDetail Detail,
    [property: JsonPropertyName("cdp_deployment_id")]
    string? CdpDeploymentId,
    [property: JsonPropertyName("deployed_by")]
    string? DeployedBy
);

public sealed record EcsDeploymentLambdaDetail(
    [property: JsonPropertyName("eventType")]
    string EventType,
    [property: JsonPropertyName("eventName")]
    string? EventName,
    [property: JsonPropertyName("deploymentId")]
    string? EcsDeploymentId,
    [property: JsonPropertyName("reason")]
    string? Reason
);
