using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Snow.Models;

public static class SnowPayloadDefaults
{
    public const string Unknown = "UNKNOWN";
}

public class SnowPortalPayload
{
    [JsonPropertyName("service")]
    public required string Service { get; init; }

    [JsonPropertyName("version")]
    public required string Version { get; init; }

    [JsonPropertyName("environment")]
    public required string Environment { get; init; }

    [JsonPropertyName("user")]
    public SnowPortalUserPayload? User { get; init; }

    [JsonPropertyName("cdpDeploymentId")]
    public required string CdpDeploymentId { get; init; }

    [JsonPropertyName("lastDeploymentStatus")]
    public required string LastDeploymentStatus { get; init; }

    [JsonPropertyName("updated")]
    public required DateTime Updated { get; init; }
}

public class SnowPortalUserPayload
{
    [JsonPropertyName("displayName")]
    public required string DisplayName { get; init; }
}

public class SnowExtendedPayload
{
    [JsonPropertyName("team_name")]
    public required string TeamName { get; init; }

    [JsonPropertyName("previous_version")]
    public string? PreviousVersion { get; init; }

    // assignment_group is intentionally not set: cdp-deployments-snow's default ServiceNow group covers
    // every team for now, and that isn't expected to change.
}
