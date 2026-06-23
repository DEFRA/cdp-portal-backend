using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Create.Models;

public record ResourceRequestStatusResponse
{
    [JsonPropertyName("requestId")]
    public required string RequestId { get; init; }

    [JsonPropertyName("branch")]
    public string? Branch { get; init; }

    [JsonPropertyName("workflowRunUrl")]
    public string? WorkflowRunUrl { get; init; }

    [JsonPropertyName("pullRequest")]
    public ResourceRequestPullRequest? PullRequest { get; init; }
}
