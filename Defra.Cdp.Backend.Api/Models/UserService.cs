namespace Defra.Cdp.Backend.Api.Models;

public record UserServiceRecord(
    string message,
    List<UserServiceTeams> teams
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

public record UserServiceTeams(
    string name,
    string description,
    string github,
    string createdAt,
    string updatedAt,
    string teamId,
    List<UserServiceUsers> users
);

public record UserServiceUsers(
    string userId,
    string name
);