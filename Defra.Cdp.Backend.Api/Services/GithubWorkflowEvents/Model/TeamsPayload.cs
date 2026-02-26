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
    
    [JsonPropertyName("slack_channels")] public TeamSlackPayload? SlackChannels { get; init; }

    public Team ToTeam()
    {
        return new Team
        {
            TeamId = TeamId,
            TeamName = Name,
            Description = Description,
            Github = Github,
            ServiceCode = ServiceCode,
            SlackChannels = new SlackChannels {
                NonProd = SlackChannels?.NonProd,
                Prod = SlackChannels?.Prod,
                Team = SlackChannels?.Team
            } 
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
            SlackChannels = new UserServiceSlackChannels
            {
                NonProd = SlackChannels?.NonProd,
                Prod = SlackChannels?.Prod,
                Team = SlackChannels?.Team
            }
        };
    }
}

public record TeamSlackPayload
{
    [JsonPropertyName("prod")] public string? Prod { get; init; }
    [JsonPropertyName("non_prod")] public string? NonProd { get; init; }
    [JsonPropertyName("team")] public string? Team { get; init; }
}
