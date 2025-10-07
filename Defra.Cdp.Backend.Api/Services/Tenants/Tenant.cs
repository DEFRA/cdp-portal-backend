using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities.Model;

namespace Defra.Cdp.Backend.Api.Services.Tenants;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;


public class Tenant
{
    [property: JsonPropertyName("name")]
    public required string Name { get; init; }
    
    [property: JsonPropertyName("envs")]
    public Dictionary<string, CdpTenant> Envs { get; init; } = new();
    
    [property: JsonPropertyName("metadata")]
    public TenantMetadata? Metadata { get; init; }
    
    
    // Portal data, this is not populated from the external events
    [property: JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = [];
    
    [property: JsonPropertyName("created")]
    public DateTime? Created { get; init; }
    
    [property: JsonPropertyName("creator")]
    public UserDetails? Creator { get; init; }

    [property: JsonPropertyName("decommissioned")]
    public Decommission? Decommissioned { get; init; }
}



public class Alert
{
    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("type")]
    public string Type { get; set; }

    [property: JsonPropertyName("uid")]
    public string Uid { get; set; }

    [property: JsonPropertyName("annotations")]
    public AlertAnnotations? Annotations { get; set; }

}

public class AlertAnnotations
{
    [property: JsonPropertyName("description")]
    public string Description { get; set; }

    [property: JsonPropertyName("runbook_url")]
    public string RunbookUrl { get; set; }

    [property: JsonPropertyName("summary")]
    public string Summary { get; set; }

}

public class BedrockGuardrail
{
    [property: JsonPropertyName("arn")]
    public string Arn { get; set; }

    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("status")]
    public string Status { get; set; }

    [property: JsonPropertyName("version")]
    public string Version { get; set; }

}

public class BedrockInferenceModel
{
    [property: JsonPropertyName("model_arn")]
    public string ModelArn { get; set; }

}

public class BedrockInferenceProfile
{
    [property: JsonPropertyName("arn")]
    public string Arn { get; set; }

    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("status")]
    public string Status { get; set; }

    [property: JsonPropertyName("type")]
    public string Type { get; set; }

    [property: JsonPropertyName("models")]
    public List<BedrockInferenceModel> Models { get; set; }

}

public class CdpTenant
{
    [property: JsonPropertyName("urls")]
    public Dictionary<string, TenantUrl> Urls { get; set; }

    [property: JsonPropertyName("ecr_repository")]
    public TenantEcrRepository? EcrRepository { get; set; }

    [property: JsonPropertyName("s3_buckets")]
    public List<TenantS3Bucket> S3Buckets { get; set; }

    [property: JsonPropertyName("sqs_queues")]
    public List<TenantSqsQueue> SqsQueues { get; set; }

    [property: JsonPropertyName("sns_topics")]
    public List<TenantSnsTopic> SnsTopics { get; set; }

    [property: JsonPropertyName("sql_database")]
    public TenantSqlDatabase? SqlDatabase { get; set; }

    [property: JsonPropertyName("dynamodb")]
    public List<TenantDynamoDB> Dynamodb { get; set; }

    [property: JsonPropertyName("api_gateway")]
    public TenantApiGateway? ApiGateway { get; set; }

    [property: JsonPropertyName("cognito_identity_pool")]
    public TenantCognitoIdentityPool? CognitoIdentityPool { get; set; }

    [property: JsonPropertyName("bedrock_ai")]
    public TenantBedrockAI? BedrockAi { get; set; }

    [property: JsonPropertyName("tenant_config")]
    public RequestedConfig? TenantConfig { get; set; }

    [property: JsonPropertyName("logs")]
    public OpensearchDashboard? Logs { get; set; }

    [property: JsonPropertyName("metrics")]
    public List<GrafanaDashboard> Metrics { get; set; }

    [property: JsonPropertyName("alerts")]
    public List<Alert> Alerts { get; set; }

    [property: JsonPropertyName("nginx")]
    public CdpTenantNginx? Nginx { get; set; }

    [property: JsonPropertyName("squid")]
    public Squid? Squid { get; set; }

}

public class CdpTenantAndMetadata
{
    [property: JsonPropertyName("tenant")]
    public CdpTenant? Tenant { get; set; }

    [property: JsonPropertyName("metadata")]
    public TenantMetadata? Metadata { get; set; }

}

public class CdpTenantNginx
{
    [property: JsonPropertyName("servers")]
    public Dictionary<string, Server> Servers { get; set; }

}

public class GrafanaDashboard
{
    [property: JsonPropertyName("url")]
    public string Url { get; set; }

    [property: JsonPropertyName("type")]
    public string Type { get; set; }

    [property: JsonPropertyName("uid")]
    public string Uid { get; set; }

    [property: JsonPropertyName("version")]
    public int Version { get; set; }

}

public class Location
{
    [property: JsonPropertyName("path")]
    public string Path { get; set; }

    [property: JsonPropertyName("params")]
    public Dictionary<string, string> Params { get; set; }

}

public class OpensearchDashboard
{
    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("url")]
    public string Url { get; set; }

}

public class PlatformStatePayload
{
    [property: JsonPropertyName("created")]
    public string Created { get; set; }

    [property: JsonPropertyName("version")]
    public int Version { get; set; }

    [property: JsonPropertyName("environment")]
    public string Environment { get; set; }

    [property: JsonPropertyName("terraform_serials")]
    public Serials TerraformSerials { get; set; }

    [property: JsonPropertyName("tenants")]
    public Dictionary<string, CdpTenantAndMetadata> Tenants { get; set; }

}

public class RequestedConfig
{
    [property: JsonPropertyName("redis")]
    public bool Redis { get; set; }

    [property: JsonPropertyName("mongo")]
    public bool Mongo { get; set; }

    [property: JsonPropertyName("s3_buckets")]
    public List<string> S3Buckets { get; set; }

    [property: JsonPropertyName("sqs_queues")]
    public List<string> SqsQueues { get; set; }

    [property: JsonPropertyName("sns_topics")]
    public List<string> SnsTopics { get; set; }

    [property: JsonPropertyName("dynamodb")]
    public List<string> Dynamodb { get; set; }

    [property: JsonPropertyName("sql_database")]
    public bool SqlDatabase { get; set; }

    [property: JsonPropertyName("api_gateway")]
    public bool ApiGateway { get; set; }

    [property: JsonPropertyName("cognito_identity_pool")]
    public bool CognitoIdentityPool { get; set; }

    [property: JsonPropertyName("bedrock_ai")]
    public bool BedrockAi { get; set; }

}

public class Serials
{
    [property: JsonPropertyName("tfsvcinfra")]
    public int Tfsvcinfra { get; set; }

    [property: JsonPropertyName("tfvanityurl")]
    public int Tfvanityurl { get; set; }

    [property: JsonPropertyName("tfwaf")]
    public int Tfwaf { get; set; }

    [property: JsonPropertyName("tfopensearch")]
    public int Tfopensearch { get; set; }

    [property: JsonPropertyName("tfgrafana")]
    public int Tfgrafana { get; set; }

}

public class Server
{
    [property: JsonPropertyName("name")]
    public string? Name { get; set; }

    [property: JsonPropertyName("locations")]
    public Dictionary<string, Location> Locations { get; set; }

    [property: JsonPropertyName("settings")]
    public Dictionary<string, string> Settings { get; set; }

}

public class Squid
{
    [property: JsonPropertyName("ports")]
    public List<int> Ports { get; set; }

    [property: JsonPropertyName("domains")]
    public List<string> Domains { get; set; }

}

public class TenantApiGateway
{
    [property: JsonPropertyName("arn")]
    public string Arn { get; set; }

    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("type")]
    public string Type { get; set; }

    [property: JsonPropertyName("minimum_compression_size")]
    public int MinimumCompressionSize { get; set; }

}

public class TenantBedrockAI
{
    [property: JsonPropertyName("inference_profiles")]
    public List<BedrockInferenceProfile> InferenceProfiles { get; set; }

    [property: JsonPropertyName("guardrails")]
    public List<BedrockGuardrail> Guardrails { get; set; }

}

public class TenantCognitoIdentityPool
{
    [property: JsonPropertyName("arn")]
    public string Arn { get; set; }

    [property: JsonPropertyName("developer_provider_name")]
    public string DeveloperProviderName { get; set; }

    [property: JsonPropertyName("identity_pool_name")]
    public string IdentityPoolName { get; set; }

}

public class TenantDynamoDB
{
    [property: JsonPropertyName("arn")]
    public string Arn { get; set; }

    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("hash_key")]
    public string? HashKey { get; set; }

    [property: JsonPropertyName("range_key")]
    public string? RangeKey { get; set; }

//    [property: JsonPropertyName("local_secondary_index")]
//    public object? LocalSecondaryIndex { get; set; }

//    [property: JsonPropertyName("global_secondary_index")]
//    public object? GlobalSecondaryIndex { get; set; }

    [property: JsonPropertyName("table_class")]
    public string? TableClass { get; set; }

    [property: JsonPropertyName("read_capacity")]
    public int ReadCapacity { get; set; }

    [property: JsonPropertyName("write_capacity")]
    public int WriteCapacity { get; set; }

    [property: JsonPropertyName("attributes")]
    public List<TenantDynamoDBAttributes>? Attributes { get; set; }

    [property: JsonPropertyName("ttl")]
    public List<TenantDynamoDBTTL>? Ttl { get; set; }

}

public class TenantDynamoDBAttributes
{
    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("type")]
    public string Type { get; set; }

}

public class TenantDynamoDBTTL
{
    [property: JsonPropertyName("attribute_name")]
    public string AttributeName { get; set; }

    [property: JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

}

public class TenantEcrRepository
{
    [property: JsonPropertyName("arn")]
    public string Arn { get; set; }

    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("url")]
    public string Url { get; set; }

}

public class TenantMetadata
{
    [property: JsonPropertyName("created")]
    public string Created { get; set; }

    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("service_code")]
    public string ServiceCode { get; set; }

    [property: JsonPropertyName("subtype")]
    public string Subtype { get; set; }

    [property: JsonPropertyName("type")]
    public string Type { get; set; }

    [property: JsonPropertyName("teams")]
    public List<string> Teams { get; set; }

}

public class TenantS3Bucket
{
    [property: JsonPropertyName("arn")]
    public string Arn { get; set; }

    [property: JsonPropertyName("bucket_name")]
    public string BucketName { get; set; }

    [property: JsonPropertyName("bucket_domain_name")]
    public string? BucketDomainName { get; set; }

    [property: JsonPropertyName("versioning")]
    public string Versioning { get; set; }

}

public class TenantSnsTopic
{
    [property: JsonPropertyName("arn")]
    public string Arn { get; set; }

    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("fifo_topic")]
    public bool FifoTopic { get; set; }

    [property: JsonPropertyName("content_based_deduplication")]
    public bool ContentBasedDeduplication { get; set; }

}

public class TenantSqlDatabase
{
    [property: JsonPropertyName("arn")]
    public string Arn { get; set; }

    [property: JsonPropertyName("endpoint")]
    public string Endpoint { get; set; }

    [property: JsonPropertyName("reader_endpoint")]
    public string ReaderEndpoint { get; set; }

    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("port")]
    public int Port { get; set; }

    [property: JsonPropertyName("engine_version")]
    public string EngineVersion { get; set; }

    [property: JsonPropertyName("engine")]
    public string Engine { get; set; }

    [property: JsonPropertyName("database_name")]
    public string DatabaseName { get; set; }

}

public class TenantSqsQueue
{
    [property: JsonPropertyName("arn")]
    public string Arn { get; set; }

    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("url")]
    public string Url { get; set; }

    [property: JsonPropertyName("fifo_queue")]
    public bool FifoQueue { get; set; }

    [property: JsonPropertyName("content_based_deduplication")]
    public bool ContentBasedDeduplication { get; set; }

    [property: JsonPropertyName("receive_wait_time_seconds")]
    public int ReceiveWaitTimeSeconds { get; set; }

    [property: JsonPropertyName("subscriptions")]
    public List<string> Subscriptions { get; set; }

}

public class TenantUrl
{
    [property: JsonPropertyName("type")]
    public string? Type { get; set; }

    [property: JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [property: JsonPropertyName("shuttered")]
    public bool Shuttered { get; set; }

}