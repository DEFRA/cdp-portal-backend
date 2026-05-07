using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Create.Models;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "resourceType")]
[JsonDerivedType(typeof(CreateS3BucketRequest), "s3")]
public abstract record CreateResourceRequest
{
    [JsonPropertyName("runId")]
    public string? RunId { get; init; }
    
    [JsonPropertyName("useBranch")]
    public string? UseBranch { get; init; }
    
    [JsonPropertyName("prTitle")]
    public string? PrTitle { get; init; }

    public abstract GenericCdpWorkflowInputs ToWorkflowInputs();
}

public record CreateS3BucketRequest : CreateResourceRequest
{
    [JsonPropertyName("service")]
    public required string Service { get; init; }

    [JsonPropertyName("bucketName")]
    public required string BucketName  { get; init; }
    
    [JsonPropertyName("environment")]
    public required string Environment  { get; init; }


    public override GenericCdpWorkflowInputs ToWorkflowInputs()
    {
        List<string> commands = [
            $"tenant s3-buckets add --environment {Environment} --service-name {Service} --bucket-name {BucketName}"
        ];
        return new GenericCdpWorkflowInputs(commands, RunId, UseBranch, PrTitle);
    }
}