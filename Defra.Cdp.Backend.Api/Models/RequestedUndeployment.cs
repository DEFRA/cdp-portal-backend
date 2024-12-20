using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public sealed class RequestedUndeployment
{
    [property: JsonPropertyName("service")]
    public string Service { get; init; } = default!;

    [property: JsonPropertyName("environment")]
    public string Environment { get; init; } = default!;

    [property: JsonPropertyName("user")] public UserDetails? User { get; init; }

    [property: JsonPropertyName("undeploymentId")]
    public string UndeploymentId { get; init; } = default!;

}
