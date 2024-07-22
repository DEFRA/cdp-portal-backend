using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

/**
 * A cut down version of TenantSecrets
 */
public record DeploymentSecrets
{
    [JsonPropertyName("keys")] public List<string> Keys { get; init; } = new();
    [JsonPropertyName("lastChangedDate")] public string LastChangedDate { get; init; } = "";
    [JsonPropertyName("createdDate")] public string CreatedDate { get; init; } = "";
}