using System.Collections.Immutable;
using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Create.Models;

public record CreateTenantResourceRequest
{
    [JsonPropertyName("s3_buckets")]
    public ImmutableArray<CreateTenantS3Bucket> S3Buckets { get; init; } = [];
    
    [JsonPropertyName("sqs_queues")]
    public ImmutableArray<CreateTenantSqsQueue> SqsQueues { get; init; } = [];
    
    [JsonPropertyName("sns_topics")]
    public ImmutableArray<CreateTenantSnsTopic> SnsTopics { get; init; } = [];

    
    public GenericCdpWorkflowInputs ToWorkflowInputs(string runId, string branch, string prTitle)
    {
        var commands = new List<string>();
        commands.AddRange(S3Buckets.Select(s3 => s3.ToWorkflowCommand()));
        commands.AddRange(SqsQueues.Select(sqs => sqs.ToWorkflowCommand()));
        commands.AddRange(SnsTopics.Select(sqs => sqs.ToWorkflowCommand()));
        
        return new GenericCdpWorkflowInputs(commands, runId, branch, prTitle);
    }

    public List<string> GetServices()
    {
        var services = new HashSet<string>();
        services.UnionWith(SqsQueues.Select(s => s.Service));
        services.UnionWith(SnsTopics.Select(s => s.Service));
        services.UnionWith(S3Buckets.Select(s => s.Service));
        return services.ToList();
    }
}

public record CreateTenantS3Bucket
{
    [JsonPropertyName("service")]
    public required string Service { get; init; }

    [JsonPropertyName("name")]
    public required string Name  { get; init; }
    
    [JsonPropertyName("environments")]
    public required string Environments  { get; init; }

    [JsonPropertyName("versioning")] public bool Versioning { get; init; } = false;
    
    public string ToWorkflowCommand()
    {
        var versioning = Versioning ? "--versioning" : "";
        return $"tenant s3-buckets add --service-name {Service} --bucket-name {Name} --environment {Environments} {versioning}".TrimEnd();
    }
}

public record CreateTenantSqsQueue
{
    [JsonPropertyName("service")]
    public required string Service { get; init; }

    [JsonPropertyName("name")]
    public required string Name  { get; init; }

    [JsonPropertyName("fifo")] 
    public bool Fifo { get; init; } = false;
    
    [JsonPropertyName("visibilityTimeout")] 
    public int? VisibilityTimeout { get; init; }
    
    [JsonPropertyName("environments")]
    public required string Environments  { get; init; }
   
    public string ToWorkflowCommand()
    {
        var queueType = Fifo ? "--queue-type fifo" : "";
        var visibilityTimeout = VisibilityTimeout != null ? $"--visibility-timeout {VisibilityTimeout}" : "";
        return $"tenant sqs-queues add --service-name {Service} --queue-names {Name} --environment {Environments} {queueType} {visibilityTimeout}".TrimEnd();
    }
}

public record CreateTenantSnsTopic
{
    [JsonPropertyName("service")]
    public required string Service { get; init; }

    [JsonPropertyName("name")]
    public required string Name  { get; init; }

    [JsonPropertyName("fifo")] 
    public bool Fifo { get; init; } = false;
    
    [JsonPropertyName("contentDeduplication")] 
    public bool ContentDeduplication { get; init; } = false;
    
    [JsonPropertyName("environments")]
    public required string Environments  { get; init; }

    public string ToWorkflowCommand()
    {
        var environments = string.Join(" ", Environments.Select(e => $"--environment {e}"));
        var topicType = Fifo ? "--topic-type fifo" : "";
        var contentDeduplication = ContentDeduplication ? "--content-based-deduplication" : "";
        return $"tenant sns-topics add --service-name {Service} --topic-names {Name} --environment {Environments} {topicType} {contentDeduplication}".TrimEnd();
    }
}