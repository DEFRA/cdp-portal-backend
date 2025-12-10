using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Teams;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public class TeamsPayload
{
    [JsonPropertyName("teams")] public required List<TeamPayload> Teams { get; init; }
}

public record TeamPayload
{
    [JsonPropertyName("team_id")] public required string TeamId { get; init; }

    [JsonPropertyName("name")] public required string Name { get; init; }

    [JsonPropertyName("description")] public string? Description { get; init; }

    [JsonPropertyName("github")] public string? Github { get; init; }

    [JsonPropertyName("service_code")] public string? ServiceCode { get; init; }

    public Team ToTeam()
    {
        return new Team
        {
            TeamId = TeamId,
            TeamName = Name,
            Description = Description,
            Github = Github,
            ServiceCode = ServiceCode
        };
    }
    
    public UserServiceTeamSync ToUserServiceTeamSync()
    {
        return new UserServiceTeamSync
        {
            TeamId = TeamId,
            Name = Name,
            Description = Description,
            Github = Github,
            ServiceCodes = ServiceCode != null ? [ServiceCode] : null,
        };
    }
}
