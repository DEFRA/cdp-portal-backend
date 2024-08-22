using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public class ApiError(string message)
{
    [property: JsonPropertyName("message")]
    public string Message { get; init; } = message;
}