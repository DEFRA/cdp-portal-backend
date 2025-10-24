using System.Text.Json.Serialization;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Models;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

[BsonIgnoreExtraElements]
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

[BsonIgnoreExtraElements]
public class AlertAnnotations
{
    [property: JsonPropertyName("description")]
    public string Description { get; set; }

    [property: JsonPropertyName("runbook_url")]
    public string RunbookUrl { get; set; }

    [property: JsonPropertyName("summary")]
    public string Summary { get; set; }

}

[BsonIgnoreExtraElements]
public class BedrockGuardrail
{
    [property: JsonPropertyName("arn")]
    public string Arn { get; set; }

    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("version")]
    public string Version { get; set; }

}

[BsonIgnoreExtraElements]
public class CdpBedrockAI
{
    [property: JsonPropertyName("profiles")]
    public List<CdpBedrockProfile> Profiles { get; set; }

}

[BsonIgnoreExtraElements]
public class CdpBedrockProfile
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
    public List<string> Models { get; set; }

    [property: JsonPropertyName("guardrail")]
    public BedrockGuardrail? Guardrail { get; set; }

}

[BsonIgnoreExtraElements]
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
    public CdpBedrockAI? BedrockAi { get; set; }

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

[BsonIgnoreExtraElements]
public class CdpTenantAndMetadata
{
    [property: JsonPropertyName("tenant")]
    public CdpTenant? Tenant { get; set; }

    [property: JsonPropertyName("metadata")]
    public TenantMetadata? Metadata { get; set; }

    [property: JsonPropertyName("progress")]
    public CreationProgress Progress { get; set; }

}

[BsonIgnoreExtraElements]
public class CdpTenantNginx
{
    [property: JsonPropertyName("servers")]
    public Dictionary<string, NginxServer> Servers { get; set; }

}

[BsonIgnoreExtraElements]
public class CreationProgress
{
    [property: JsonPropertyName("steps")]
    public Dictionary<string, bool> Steps { get; set; }

    [property: JsonPropertyName("complete")]
    public bool Complete { get; set; }
   
}

[BsonIgnoreExtraElements]
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

[BsonIgnoreExtraElements]
public class NginxLocation
{
    [property: JsonPropertyName("path")]
    public string Path { get; set; }

    [property: JsonPropertyName("params")]
    public Dictionary<string, string> Params { get; set; }

}

[BsonIgnoreExtraElements]
public class NginxServer
{
    [property: JsonPropertyName("name")]
    public string? Name { get; set; }

    [property: JsonPropertyName("locations")]
    public Dictionary<string, NginxLocation> Locations { get; set; }

    [property: JsonPropertyName("settings")]
    public Dictionary<string, string> Settings { get; set; }

}

[BsonIgnoreExtraElements]
public class OpensearchDashboard
{
    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("url")]
    public string Url { get; set; }

}

[BsonIgnoreExtraElements]
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

[BsonIgnoreExtraElements]
public class RequestedConfig
{
    [property: JsonPropertyName("zone")]
    public string? Zone { get; set; }

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

[BsonIgnoreExtraElements]
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

[BsonIgnoreExtraElements]
public class Squid
{
    [property: JsonPropertyName("ports")]
    public List<int> Ports { get; set; }

    [property: JsonPropertyName("domains")]
    public List<string> Domains { get; set; }

}

[BsonIgnoreExtraElements]
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

[BsonIgnoreExtraElements]
public class TenantCognitoIdentityPool
{
    [property: JsonPropertyName("arn")]
    public string Arn { get; set; }

    [property: JsonPropertyName("developer_provider_name")]
    public string DeveloperProviderName { get; set; }

    [property: JsonPropertyName("identity_pool_name")]
    public string IdentityPoolName { get; set; }

}

[BsonIgnoreExtraElements]
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

[BsonIgnoreExtraElements]
public class TenantDynamoDBAttributes
{
    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("type")]
    public string Type { get; set; }

}

[BsonIgnoreExtraElements]
public class TenantDynamoDBTTL
{
    [property: JsonPropertyName("attribute_name")]
    public string AttributeName { get; set; }

    [property: JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

}

[BsonIgnoreExtraElements]
public class TenantEcrRepository
{
    [property: JsonPropertyName("arn")]
    public string Arn { get; set; }

    [property: JsonPropertyName("name")]
    public string Name { get; set; }

    [property: JsonPropertyName("url")]
    public string Url { get; set; }

}

[BsonIgnoreExtraElements]
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

    [property: JsonPropertyName("environments")]
    public List<string>? Environments { get; set; }

}

[BsonIgnoreExtraElements]
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

[BsonIgnoreExtraElements]
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

[BsonIgnoreExtraElements]
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

[BsonIgnoreExtraElements]
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

[BsonIgnoreExtraElements]
public class TenantUrl
{
    [property: JsonPropertyName("type")]
    public string? Type { get; set; }

    [property: JsonPropertyName("enabled")]
    public bool Enabled { get; set; }

    [property: JsonPropertyName("shuttered")]
    public bool Shuttered { get; set; }

}

public static class TenantDataVersion
{
    public static readonly string Version = "2ac3e38cb47b91dbb40d88316c10083dffd09708451ec2e3a522fb450d01d60d";
}

#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.