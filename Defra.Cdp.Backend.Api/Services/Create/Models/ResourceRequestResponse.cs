using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Github.Workflows;

namespace Defra.Cdp.Backend.Api.Services.Create.Models;

public record ResourceRequestResponse
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("requestedAt")]
    public DateTime RequestedAt { get; init; }

    [JsonPropertyName("requestedBy")]
    public UserDetails? RequestedBy { get; init; }

    [JsonPropertyName("modifiedAt")]
    public DateTime ModifiedAt { get; init; }
    
    [JsonPropertyName("workflow")]
    public GitHubTriggerWorkflowResponse? Workflow { get; init; }

    [JsonPropertyName("pullRequest")]
    public ResourceRequestPullRequest? PullRequest { get; init; }
    
    [JsonPropertyName("status")]
    public required string Status { get; init; }

    [JsonPropertyName("teams")] 
    public List<Team> Teams { get; init; } = [];

    [JsonPropertyName("resources")] 
    public CreateTenantResourceRequest? Resources { get; set; }


    public static ResourceRequestResponse FromRequest(ResourceRequestRecord record)
    {
        return new ResourceRequestResponse
        {
            Id = record.Id.ToString()!,
            Status = record.Status,
            PullRequest = record.PullRequest,
            Workflow = record.Workflow,
            RequestedAt = record.RequestedAt,
            RequestedBy = record.RequestedBy,
            ModifiedAt = record.ModifiedAt == DateTime.MinValue ? record.RequestedAt : record.ModifiedAt,
            Teams = record.Teams
        };
    }

    public static ResourceRequestResponse FromRequestWithResources(ResourceRequestRecord record)
    {
        var response = FromRequest(record);
        response.Resources = record.Resources;

        return response;
    }
}