namespace Defra.Cdp.Backend.Api.Models;

public record UserServiceRecord(
    string message,
    List<UserServiceTeams> teams
)
{
    public Dictionary<string, string> GithubToCdpMap { get; } = teams.ToDictionary(t => t.github, t => t.teamId);
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