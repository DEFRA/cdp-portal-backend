using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public record ServiceNowChangeRequestPayload(
    [property: JsonPropertyName("status")] string Status,

    [property: JsonPropertyName("cdpDeploymentId")] string CdpDeploymentId,

    [property: JsonPropertyName("service")] string Service,

    [property: JsonPropertyName("workflowRunUrl")] string WorkflowRunUrl,

    [property: JsonPropertyName("changeRequestNumber")] string? ChangeRequestNumber = null,

    [property: JsonPropertyName("failureStage")] string? FailureStage = null,
    
    [property: JsonPropertyName("failureReason")] string? FailureReason = null,
    
    [property: JsonPropertyName("attempt")] int? Attempt = null,
    
    [property: JsonPropertyName("maxAttempts")] int? MaxAttempts = null
);

public static class ChangeRequestStatuses
{
    // These are the statuses we can get in payload but we 
    public const string Created = "created";
    public const string Pending = "pending";
    public const string Failed = "failed";
}