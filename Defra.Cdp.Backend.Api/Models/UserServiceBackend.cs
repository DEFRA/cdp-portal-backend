using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public record UserServiceTeam(
    [property: JsonPropertyName("name")] string name,
    [property: JsonPropertyName("description")] string description,
    [property: JsonPropertyName("github")] string? github,
    [property: JsonPropertyName("createdAt")] string createdAt,
    [property: JsonPropertyName("updatedAt")] string updatedAt,
    [property: JsonPropertyName("teamId")] string teamId,
    [property: JsonPropertyName("users")] List<UserId> users
);

public record UserServiceTeamSync
{
    [property: JsonPropertyName("teamId")] public required string TeamId { get; init; }
    [property: JsonPropertyName("name")] public required string Name { get; init; }
    [property: JsonPropertyName("description")] public string? Description { get; init; }
    [property: JsonPropertyName("github")] public string? Github { get; init; }
    [property: JsonPropertyName("serviceCodes")] public IReadOnlyList<string>? ServiceCodes { get; init; }
    [property: JsonPropertyName("slackChannels")] public UserServiceSlackChannels? SlackChannels { get; init; }
}

public record UserId(
    [property: JsonPropertyName("userId")] string userId,
    [property: JsonPropertyName("name")] string name
);

public record TeamIdAndName(
    [property: JsonPropertyName("teamId")] string teamId,
    [property: JsonPropertyName("name")] string name
);

public record UserServiceUser(
    [property: JsonPropertyName("name")] string name,
    [property: JsonPropertyName("email")] string email,
    [property: JsonPropertyName("userId")] string userId,
    [property: JsonPropertyName("teams")] List<TeamIdAndName> teams
);

public record UserServiceSlackChannels
{
    [JsonPropertyName("team")] public string? Team { get; init; }
    [JsonPropertyName("prod")] public string? Prod { get; init; }
    [JsonPropertyName("nonProd")] public string? NonProd { get; init; }
}