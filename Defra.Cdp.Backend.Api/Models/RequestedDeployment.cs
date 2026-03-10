using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Models;

public sealed class UserDetails
{
    [property: JsonPropertyName("id")] public string Id { get; init; } = default!;

    [property: JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = default!;
}

public sealed class RequestedDeployment : IValidatableObject
{
    [property: JsonPropertyName("service")]
    public required string Service { get; init; } 

    [property: JsonPropertyName("version")]
    public required string Version { get; init; }

    [property: JsonPropertyName("environment")]
    public required string Environment { get; init; }

    [property: JsonPropertyName("user")] public UserDetails? User { get; init; }

    [property: JsonPropertyName("instanceCount")]
    public int InstanceCount { get; init; }

    [property: JsonPropertyName("cpu")] public string Cpu { get; init; } = default!;
    [property: JsonPropertyName("memory")] public string Memory { get; init; } = default!;

    [property: JsonPropertyName("deploymentId")]
    public string DeploymentId { get; init; } = default!;

    [property: JsonPropertyName("configVersion")]
    public string? ConfigVersion { get; init; } = default!;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!CdpEnvironments.Environments.Contains(Environment))
        {
            yield return new ValidationResult(
                $"invalid environment {Environment}",
                [nameof(Environment)]
            );
        }
    }
}