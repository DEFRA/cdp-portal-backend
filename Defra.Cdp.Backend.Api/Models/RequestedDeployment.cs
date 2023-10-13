using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public sealed record RequestedDeployment
{
    [property: JsonPropertyName("service")]
    public string Service { get; set; } = string.Empty;

    [property: JsonPropertyName("version")]
    public string Version { get; set; } = string.Empty;

    [property: JsonPropertyName("environment")]
    public string Environment { get; set; } = string.Empty;

    [property: JsonPropertyName("user")] public string? User { get; set; }
    
    [property: JsonPropertyName("userId")] public string? UserId { get; set; }
}