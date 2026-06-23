using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public record ResourceRequestPrPayload
{
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    [JsonPropertyName("workflowRunId")]
    public required string WorkflowRunId { get; init; }

    [JsonPropertyName("workflowRunUrl")]
    public required string WorkflowRunUrl { get; init; }

    [JsonPropertyName("repository")]
    public required string Repository { get; init; }

    [JsonPropertyName("branch")]
    public string? Branch { get; init; }

    [JsonPropertyName("prUrl")]
    public required string PrUrl { get; init; }

    [JsonPropertyName("prNumber")]
    public required int PrNumber { get; init; }
}
