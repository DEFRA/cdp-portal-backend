using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Github.Workflows;

namespace Defra.Cdp.Backend.Api.Services.Snow.Models;

/// <summary>
/// Inputs for the "ServiceNow Deployment Record" workflow_dispatch in cdp-deployments-snow.
/// </summary>
/// <remarks>
/// portal_payload and extended_payload are stringified JSON, not nested objects, because
/// GitHub Actions workflow_dispatch only accepts flat string/boolean/choice inputs - it does not
/// support nested JSON. Packing the whole deployment record into one string input also avoids
/// needing to add a new workflow input (and a matching change in cdp-deployments-snow) every time
/// a new field is needed; cdp-deployments-snow parses these two strings back into structured
/// payloads itself (see PAYLOAD.md). Same pattern as GenericCdpWorkflowInputs.Commands.
/// </remarks>
public class SnowDeploymentWorkflowInputs(string portalPayload, string? extendedPayload) : IGithubWorkflowInputs
{
    [JsonPropertyName("portal_payload")]
    public string PortalPayload { get; init; } = portalPayload;

    [JsonPropertyName("extended_payload")]
    public string? ExtendedPayload { get; init; } = extendedPayload;
}
