using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public class ApiError
{
    [property: JsonPropertyName("message")]
    public string Message { get; init; }

    public ApiError(string message)
    {
        Message = message;
    }
}