using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public class ApiError
{
    [JsonPropertyName("message")]
    public string Message { get; init; }

    [JsonPropertyName("errors")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? Errors { get; init; } 
    
    public ApiError(string message)
    {
        Message = message;
    }
    
    public ApiError(string message, List<string> errors)
    {
        Message = message;
        Errors = errors;
    }
}