using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Services.Users;

[BsonIgnoreExtraElements]
public record User
{
    [JsonPropertyName("userId")] public required string UserId { get; init; }
    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("email")] public required string Email { get; init; }
    [JsonPropertyName("github")] public string? Github { get; init; }
}