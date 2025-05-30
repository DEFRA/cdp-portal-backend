using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public record UserServiceTeamResponse(
    string message,
    List<UserServiceTeam> teams
)
{
    public Dictionary<string, string> GithubToTeamIdMap { get; } =
        teams
            .Where(t => !string.IsNullOrWhiteSpace(t.github))
            .DistinctBy(t => t.github)
            .ToDictionary(t => t.github, t => t.teamId);

    public Dictionary<string, string> GithubToTeamNameMap { get; } =
        teams
            .Where(t => !string.IsNullOrWhiteSpace(t.github))
            .DistinctBy(t => t.github)
            .ToDictionary(t => t.github, t => t.name);
}

public record UserServiceTeam(
    [property: JsonPropertyName("name")] string name,
    [property: JsonPropertyName("description")] string description,
    [property: JsonPropertyName("github")] string? github,
    [property: JsonPropertyName("createdAt")] string createdAt,
    [property: JsonPropertyName("updatedAt")] string updatedAt,
    [property: JsonPropertyName("teamId")] string teamId,
    [property: JsonPropertyName("users")] List<UserId> users
);

public record UserId(
    [property: JsonPropertyName("userId")] string userId,
    [property: JsonPropertyName("name")] string name
);

public record TeamId(
    [property: JsonPropertyName("teamId")] string teamId,
    [property: JsonPropertyName("name")] string name
);

public record UserServiceUserResponse(
    [property: JsonPropertyName("message")] string message,
    [property: JsonPropertyName("user")] UserServiceUser? user
);

public record UserServiceUser(
    [property: JsonPropertyName("name")] string name,
    [property: JsonPropertyName("email")] string email,
    [property: JsonPropertyName("userId")] string userId,
    [property: JsonPropertyName("teams")] List<TeamId> teams
);