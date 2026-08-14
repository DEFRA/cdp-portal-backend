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

    // TODO: assignment_group is not set, so every team's deployments currently land in
    // cdp-deployments-snow's single default ServiceNow group. Clarify whether teams need their
    // own assignment group, and if so, where that sys_id should be sourced from.
}
