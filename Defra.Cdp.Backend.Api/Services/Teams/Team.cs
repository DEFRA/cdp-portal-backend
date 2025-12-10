using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Users;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Services.Teams;

[BsonIgnoreExtraElements]
public record Team
{
    [JsonPropertyName("teamId")] public required string TeamId { get; init; }
    [JsonPropertyName("name")] public required string TeamName { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
    [JsonPropertyName("serviceCode")] public string? ServiceCode { get; init; }
    [JsonPropertyName("github")] public string? Github { get; init; }
    [JsonPropertyName("created")] public DateTime? Created { get; init; }
};
