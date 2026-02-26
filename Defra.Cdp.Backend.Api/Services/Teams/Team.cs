using System.Text.Json.Serialization;
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
    [JsonPropertyName("slackChannels")] public SlackChannels? SlackChannels { get; init; }
}

[BsonIgnoreExtraElements]
public record SlackChannels
{
    [JsonPropertyName("team")] public string? Team { get; init; }
    [JsonPropertyName("prod")] public string? Prod { get; init; }
    [JsonPropertyName("nonProd")] public string? NonProd { get; init; }
}