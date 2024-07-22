using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public record SecretKey
{
    [JsonPropertyName("keys")] public List<string> Keys { get; init; } = new();
    [JsonPropertyName("lastChangedDate")] public string LastChangedDate { get; init; } = "";
    [JsonPropertyName("createdDate")] public string CreatedDate { get; init; } = "";
}