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
    [property: JsonPropertyName("cdp_request")]
    EcsDeploymentLambdaRequest? Request
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

public sealed record EcsConfigFile(
    [property: JsonPropertyName("value")]
    string Value,
    [property: JsonPropertyName("type")]
    string Type
    );

public sealed record EcsDeployedBy(
    string user_id,
    string display_name
    );

public sealed record EcsDeploymentLambdaRequest(
    [property: JsonPropertyName("container_image")]
    string ContainerImage,
    [property: JsonPropertyName("container_version")]
    string ContainerVersion,
    [property: JsonPropertyName("desired_count")]
    int DesiredCount,
    [property: JsonPropertyName("env_files")]
    List<EcsConfigFile> EnvFiles,
    [property: JsonPropertyName("task_cpu")]
    int TaskCpu,
    [property: JsonPropertyName("task_memory")]
    int TaskMemory,
    [property: JsonPropertyName("environment")]
    string Environment,
    [property: JsonPropertyName("deployed_by")]
    EcsDeployedBy DeployedBy
);