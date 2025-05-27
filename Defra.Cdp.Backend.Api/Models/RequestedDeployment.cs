using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public sealed class UserDetails
{
    [property: JsonPropertyName("id")] public string Id { get; init; } = default!;

    [property: JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = default!;
}

public sealed class RequestedDeployment
{
    [property: JsonPropertyName("service")]
    public string Service { get; init; } = default!;

    [property: JsonPropertyName("version")]
    public string Version { get; init; } = default!;

    [property: JsonPropertyName("environment")]
    public string Environment { get; init; } = default!;

    [property: JsonPropertyName("user")] public UserDetails? User { get; init; }

    [property: JsonPropertyName("instanceCount")]
    public int InstanceCount { get; init; }

    [property: JsonPropertyName("cpu")] public string Cpu { get; init; } = default!;
    [property: JsonPropertyName("memory")] public string Memory { get; init; } = default!;

    [property: JsonPropertyName("deploymentId")]
    public string DeploymentId { get; init; } = default!;

    [property: JsonPropertyName("configVersion")]
    public string? ConfigVersion { get; init; } = default!;
}