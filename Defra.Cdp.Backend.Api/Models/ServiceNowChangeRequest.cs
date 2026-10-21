using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public class ServiceNowChangeRequest
{
    [JsonPropertyName("status")]
    public required string Status { get; set; } 
    
    [JsonPropertyName("workflowRunUrl")]
    public string? WorkflowRunUrl { get; set; }

    [JsonPropertyName("changeRequestNumber")]
    public string? ChangeRequestNumber { get; set; }
    
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public required string CdpDeploymentId { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWriting)]
    public required string Service { get; init; }

    [JsonIgnore]
    public string? FailureStage { get; set; }

    [JsonIgnore]
    public string? FailureReason { get; set; }

    [JsonIgnore]
    public int? Attempt { get; set; }

    [JsonIgnore]
    public int? MaxAttempts { get; set; }
}