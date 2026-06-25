using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Create.Models;

public record ResourceRequestResponse
{
    [JsonPropertyName("workflow")]
    public GitHubTriggerWorkflowResponse? Workflow { get; init; }

    [JsonPropertyName("pullRequest")]
    public ResourceRequestPullRequest? PullRequest { get; init; }
}