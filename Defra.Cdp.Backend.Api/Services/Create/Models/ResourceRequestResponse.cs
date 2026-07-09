using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities.Model;

namespace Defra.Cdp.Backend.Api.Services.Create.Models;

public record ResourceRequestResponse
{
    [JsonPropertyName("requestedAt")]
    public DateTime RequestedAt { get; init; }

    [JsonPropertyName("requestedBy")]
    public UserDetails? RequestedBy { get; init; }
    
    [JsonPropertyName("workflow")]
    public GitHubTriggerWorkflowResponse? Workflow { get; init; }

    [JsonPropertyName("pullRequest")]
    public ResourceRequestPullRequest? PullRequest { get; init; }
    
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("teams")] public List<Team> Teams { get; init; } = [];

    
    public static ResourceRequestResponse FromRequest(ResourceRequestRecord record)
    {
        return new ResourceRequestResponse
        {
            Status = record.Status,
            PullRequest = record.PullRequest,
            Workflow = record.Workflow,
            RequestedAt = record.RequestedAt,
            RequestedBy = record.RequestedBy,
            Teams = record.Teams
        };
    }
}