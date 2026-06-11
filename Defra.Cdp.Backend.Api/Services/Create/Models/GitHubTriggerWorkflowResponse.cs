using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Create.Models;

public record GitHubTriggerWorkflowResponse
{
    [JsonPropertyName("workflow_run_id")]
    public long? WorkflowRunId { get; init; }

    [JsonPropertyName("run_url")]
    public string? WorkflowRunUrl { get; init; }
    
    [JsonPropertyName("html_url")]
    public string? WorkflowRunHtmlUrl { get; init; }
}