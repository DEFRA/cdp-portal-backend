using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Create.Models;

public record S3BucketRequest
{
    [JsonPropertyName("service")]
    public required string Service { get; init; }

    [JsonPropertyName("bucketName")]
    public required string BucketName  { get; init; }
    
    [JsonPropertyName("environment")]
    public required string Environment  { get; init; }
    
    [JsonPropertyName("runId")]
    public string? RunId { get; init; }
    
    [JsonPropertyName("useBranch")]
    public string? UseBranch { get; init; }
    
    [JsonPropertyName("prTitle")]
    public string? PrTitle { get; init; }
    
    
    public GenericCdpWorkflowInputs ToWorkflowInputs()
    {
        List<string> commands = [
            $"tenant s3-buckets add --environment {Environment} --service-name {Service} --bucket-name {BucketName}"
        ];
        return new GenericCdpWorkflowInputs(commands, RunId, UseBranch, PrTitle);
    }
}