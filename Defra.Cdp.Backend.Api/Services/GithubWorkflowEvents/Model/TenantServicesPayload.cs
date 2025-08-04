using System.Text.Json.Serialization;
using Amazon.S3.Model;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public class TenantServicesPayload
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }
    [JsonPropertyName("services")] public required List<Service> Services { get; init; }
}

public record Service
{
    
    public record SqsSubscription
    {
        [JsonPropertyName("topics")] public List<string> Topics { get; init; } = [];
        [JsonPropertyName("filter_enabled")] public bool? FilterEnabled { get; init; }
        [JsonPropertyName("filter_policy")] public string? FilterPolicy { get; init; }
    }

    public record SqsQueue
    {
        [JsonPropertyName("name")] public required string Name { get; init; }
        [JsonPropertyName("cross_account_allow_list")] public List<string>? CrossAccountAllowList { get; init; }
        [JsonPropertyName("dlq_max_receive_count")] public int? DlqMaxReceiveCount { get; init; }
        
        // TODO: fix the underlying data as its currently quoted
        //[JsonPropertyName("fifo_queue")] public bool? FifoQueue { get; init; } = false;
        
        [JsonPropertyName("content_based_deduplication")] public bool? ContentBasedDeduplication { get; init; } = false;
        [JsonPropertyName("visibility_timeout_seconds")] public int? VisibilityTimeoutSeconds { get; init; }
        [JsonPropertyName("subscriptions")] public List<SqsSubscription> Subscriptions { get; init; } = [];
    }

    public record SnsTopic
    {
        [JsonPropertyName("name")] public required string Name { get; init; }
        [JsonPropertyName("cross_account_allow_list")] public List<string>? CrossAccountAllowList  { get; init; }
        
        // TODO: fix the underlying data as its currently quoted
        // [JsonPropertyName("fifo_topic")] public bool? FifoTopic { get; init; } = false;
        [JsonPropertyName("content_based_deduplication")] public bool? ContentBasedDeduplication { get; init; } = false;
    }

    public record S3Bucket
    {
        [JsonPropertyName("name")] public required string Name { get; init; }
        [JsonPropertyName("versioning")] public string? Versioning { get; init; }
    }

    [JsonPropertyName("name")] public required string Name { get; init; }
    [JsonPropertyName("zone")] public required string Zone { get; init; }
    [JsonPropertyName("mongo")] public required bool Mongo { get; init; }
    [JsonPropertyName("redis")] public required bool Redis { get; init; }
    [JsonPropertyName("rds_aurora_postgres")] public bool Postgres { get; init; }
    [JsonPropertyName("service_code")] public required string ServiceCode { get; init; }
    [JsonPropertyName("test_suite")] public string? TestSuite { get; init; }
    
    // Soon to be deprecated bucket/queue data
    [JsonPropertyName("buckets")] public List<string>? Buckets { get; init; }
    [JsonPropertyName("queues")] public List<string>? Queues { get; init; }
    
    // New style infra details
    [JsonPropertyName("s3_buckets")] public List<S3Bucket>? S3Buckets { get; init; }
    [JsonPropertyName("sqs_queues")] public List<SqsQueue>? SqsQueues { get; init; }
    [JsonPropertyName("sns_topics")] public List<SnsTopic>? SnsTopics { get; init; }
    
    [JsonPropertyName("api_enabled")] public bool? ApiEnabled { get; init; }
    [JsonPropertyName("api_type")] public string? ApiType { get; init; }
}