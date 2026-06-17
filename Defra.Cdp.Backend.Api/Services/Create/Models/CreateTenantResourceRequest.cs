using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Create.Models;

public record CreateTenantResourceRequest
{
    [JsonPropertyName("s3_buckets")]
    public List<CreateTenantS3Bucket> S3Buckets { get; init; } = [];
    
    [JsonPropertyName("sqs_queues")]
    public List<CreateTenantSqsQueue> SqsQueues { get; init; } = [];
    
    [JsonPropertyName("sns_topics")]
    public List<CreateTenantSnsTopic> SnsTopics { get; init; } = [];

    [JsonPropertyName("sqs_sns_subscriptions")]
    public List<CreateTenantSubscription> Subscriptions { get; init; } = [];

    
    public GenericCdpWorkflowInputs ToWorkflowInputs(string runId, string branch, string prTitle)
    {
        var commands = new List<string>();
        commands.AddRange(S3Buckets.Select(s3 => s3.ToWorkflowCommand()));
        commands.AddRange(SqsQueues.Select(sqs => sqs.ToWorkflowCommand()));
        commands.AddRange(SnsTopics.Select(sns => sns.ToWorkflowCommand()));
        commands.AddRange(Subscriptions.Select(sub => sub.ToWorkflowCommand(SnsTopics)));
        return new GenericCdpWorkflowInputs(commands, runId, branch, prTitle);
    }

    public List<string> GetServices()
    {
        var services = new HashSet<string>();
        services.UnionWith(SqsQueues.Select(s => s.Service));
        services.UnionWith(SnsTopics.Select(s => s.Service));
        services.UnionWith(S3Buckets.Select(s => s.Service));
        services.UnionWith(Subscriptions.Select(s => s.QueueService));
        services.UnionWith(Subscriptions.Select(s => s.TopicService));
        return services.ToList();
    }

    public int Count()
    {
        return S3Buckets.Count + SqsQueues.Count + SnsTopics.Count + Subscriptions.Count;
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

    [JsonPropertyName("contentBasedDeduplication")]
    public bool ContentBasedDeduplication { get; init; }
    
    [JsonPropertyName("deduplicationScope")]
    public string? DeduplicationScope { get; init; }
    
    [JsonPropertyName("fifoThroughputLimit")]
    public string? FifoThroughputLimit { get; init; }
    
    [JsonPropertyName("dlqMaxReceiveCount")]
    public int? DqlMaxReceiveCount { get; init; }

    [JsonPropertyName("redriveAllowPolicyByQueue")]
    public bool RedriveAllowPolicyByQueue { get; init; }
    
    [JsonPropertyName("environments")]
    public required string Environments  { get; init; }
   
    public string ToWorkflowCommand()
    {
        List<string> optionalParams = [
            Fifo ? "--queue-type fifo" : "",
            VisibilityTimeout == null ? "" : $"--visibility-timeout {VisibilityTimeout}",
            FifoThroughputLimit == null ? "" : $"--fifo-throughput-limit {FifoThroughputLimit}",
            DqlMaxReceiveCount == null ? "" : $"--dlq-max-receive-count {DqlMaxReceiveCount}",
            DeduplicationScope == null ? "" : $"--deduplication-scope {DeduplicationScope}",
            RedriveAllowPolicyByQueue ? "--redrive-allow-policy-by-queue" : "",
            ContentBasedDeduplication ? "--content-based-deduplication" : ""
        ];
        
        return $"tenant sqs-queues add --service-name {Service} --queue-names {Name} --environment {Environments} {string.Join(" ", optionalParams)}".TrimEnd();
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
        var topicType = Fifo ? "--topic-type fifo" : "";
        var contentDeduplication = ContentDeduplication ? "--content-based-deduplication" : "";
        return $"tenant sns-topics add --service-name {Service} --topic-names {Name} --environment {Environments} {topicType} {contentDeduplication}".TrimEnd();
    }
}

public record CreateTenantSubscription
{
    [JsonPropertyName("queueService")]
    public required string QueueService { get; init; }

    [JsonPropertyName("queue")]
    public required string Queue { get; init; }

    [JsonPropertyName("topicService")]
    public required string TopicService { get; init; }

    [JsonPropertyName("topic")]
    public required string Topic { get; set; }

    [JsonPropertyName("environments")]
    public required string Environments  { get; init; }
    
    public string ToWorkflowCommand(List<CreateTenantSnsTopic> topics)
    {
        // If the topic is part of the same request and its fifo, update the topic name with suffix.
        // The workflow requires us to pass the 'full' topic name, including fifo suffix which is only added
        // after its created.
        var finalTopic = Topic;
        
        foreach (var subTopic in topics)
        {
            if (Topic.EndsWith(".fifo")) break;
            if (!subTopic.Fifo) continue;
            if (subTopic.Name == Topic)
            {
                finalTopic = $"{Topic}.fifo";
                break;
            }
        }
        
        return $"tenant sqs-queues subscriptions add --environment {Environments} --service {QueueService} --queue-name {Queue} --topic-full-name {finalTopic}".TrimEnd();
    }
}