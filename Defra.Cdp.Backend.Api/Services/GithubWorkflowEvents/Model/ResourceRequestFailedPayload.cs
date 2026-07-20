using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public record ResourceRequestFailedPayload
{
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    [JsonPropertyName("workflowRunId")]
    public string? WorkflowRunId { get; init; }

    [JsonPropertyName("workflowRunUrl")]
    public string? WorkflowRunUrl { get; init; }
}
