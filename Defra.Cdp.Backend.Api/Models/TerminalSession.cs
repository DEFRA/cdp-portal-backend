using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Models;

[BsonIgnoreExtraElements]
public class TerminalSession
{
    [property: JsonPropertyName("token")]
    public required string Token { get; init; }

    [property: JsonPropertyName("environment")]
    public required string Environment { get; init; }

    [property: JsonPropertyName("service")]
    public required string Service { get; init; }

    [property: JsonPropertyName("user")]
    public required UserDetails User { get; set; }

    public DateTime Requested { get; set; } = DateTime.UtcNow;
}