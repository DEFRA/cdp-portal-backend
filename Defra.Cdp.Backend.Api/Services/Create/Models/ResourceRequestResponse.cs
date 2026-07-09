using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Create.Models;

public record ResourceRequestResponse
{
    [JsonPropertyName("requestedAt")]
    public DateTime RequestedAt { get; init; }

    [JsonPropertyName("workflow")]
    public GitHubTriggerWorkflowResponse? Workflow { get; init; }

    [JsonPropertyName("pullRequest")]
    public ResourceRequestPullRequest? PullRequest { get; init; }
    
    [JsonPropertyName("status")]
    public required string Status { get; init; }

}