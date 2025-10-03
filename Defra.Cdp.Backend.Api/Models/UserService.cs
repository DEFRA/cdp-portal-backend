using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public record UserServiceTeam(
    [property: JsonPropertyName("name")] string name,
    [property: JsonPropertyName("description")] string description,
    [property: JsonPropertyName("github")] string? github,
    [property: JsonPropertyName("createdAt")] string createdAt,
    [property: JsonPropertyName("updatedAt")] string updatedAt,
    [property: JsonPropertyName("teamId")] string teamId,
    [property: JsonPropertyName("users")] List<UserId> users,
    [property: JsonPropertyName("teamId")] List<string> serviceCode
);

public record UserId(
    [property: JsonPropertyName("userId")] string userId,
    [property: JsonPropertyName("name")] string name
);

public record Team(
    [property: JsonPropertyName("teamId")] string teamId,
    [property: JsonPropertyName("name")] string name
);

public record UserServiceUser(
    [property: JsonPropertyName("name")] string name,
    [property: JsonPropertyName("email")] string email,
    [property: JsonPropertyName("userId")] string userId,
    [property: JsonPropertyName("teams")] List<Team> teams
);