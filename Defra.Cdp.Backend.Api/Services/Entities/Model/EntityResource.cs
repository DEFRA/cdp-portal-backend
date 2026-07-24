using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;

namespace Defra.Cdp.Backend.Api.Services.Entities.Model;

public record EntityResourceType(string Name, string Icon);


// Available icons https://icones.js.org/collection/logos
public record EntityResource<T>(
    string Resource,
    string Icon,
    string Name,
    T Properties
)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResourceRequestId { get; set; } = null;
}

public record EntityResources
{
    [JsonPropertyName("s3_buckets")] public List<EntityResource<TenantS3Bucket>> S3Buckets { get; set; } = [];
    [JsonPropertyName("sqs_queues")] public List<EntityResource<TenantSqsQueue>> SqsQueues { get; set; } = [];
    [JsonPropertyName("sns_topics")] public List<EntityResource<TenantSnsTopic>> SnsTopics { get; set; } = [];
    [JsonPropertyName("sql_database")] public List<EntityResource<TenantSqlDatabase>> SqlDatabase { get; set; } = [];
    [JsonPropertyName("dynamodb")] public List<EntityResource<TenantDynamoDB>> Dynamodb { get; set; } = [];
    [JsonPropertyName("api_gateways")] public List<EntityResource<TenantApiGateway>> ApiGateways { get; set; } = [];
    [JsonPropertyName("cognito_identity_pool")] public List<EntityResource<TenantCognitoIdentityPool>> CognitoIdentityPool { get; set; } = [];
    [JsonPropertyName("bedrock_ai")] public List<EntityResource<CdpBedrockProfile>> BedrockAi { get; set; } = [];
}

public static class EntityResourceMapper
{
    public static readonly EntityResourceType SQS = new("sqs", "aws-sqs");
    public static readonly EntityResourceType SNS = new("sns", "aws-sns");
    public static readonly EntityResourceType S3 = new("s3", "aws-s3");
    public static readonly EntityResourceType SQL = new("sql", "aws-rds");
    public static readonly EntityResourceType DynamoDB = new("dynamodb", "aws-dynamodb");
    public static readonly EntityResourceType ApiGateway = new("s3", "aws-api-gateway");
    public static readonly EntityResourceType Cognito = new("cognito", "aws-cognito");
    public static readonly EntityResourceType Bedrock = new("bedrock", "aws-bedrock");

    public static EntityResource<TenantS3Bucket> Map(TenantS3Bucket s3) => new(S3.Name, S3.Icon, s3.BucketName, s3);
    public static EntityResource<TenantSqsQueue> Map(TenantSqsQueue sqs) => new(SQS.Name, SQS.Icon, sqs.Name, sqs);
    public static EntityResource<TenantSnsTopic> Map(TenantSnsTopic sns) => new(SNS.Name, SNS.Icon, sns.Name, sns);
    public static EntityResource<TenantDynamoDB> Map(TenantDynamoDB d) => new(DynamoDB.Name, DynamoDB.Icon, d.Name, d);
    public static EntityResource<TenantApiGateway> Map(TenantApiGateway api) => new(ApiGateway.Name, ApiGateway.Icon, api.Name, api);
    public static EntityResource<TenantSqlDatabase> Map(TenantSqlDatabase sql) => new(SQL.Name, SQL.Icon, sql.Name, sql);
    public static EntityResource<TenantCognitoIdentityPool> Map(TenantCognitoIdentityPool cog) => new(Cognito.Name, Cognito.Icon, cog.IdentityPoolName, cog);
    public static EntityResource<CdpBedrockProfile> Map(CdpBedrockProfile ai) => new(Bedrock.Name, Bedrock.Icon, ai.Name, ai);

    public static EntityResource<TenantS3Bucket> Map(CreateTenantS3Bucket s3, string resourceRequestId, string env) => new(S3.Name, S3.Icon, ExpandBucketName(s3.Name, env), new TenantS3Bucket
    {
        BucketName = ExpandBucketName(s3.Name, env),
        Versioning = s3.Versioning ? "Enabled" : "Disabled"
    })
    {
        ResourceRequestId = resourceRequestId
    };

    public static EntityResource<TenantSqsQueue> Map(CreateTenantSqsQueue sqs, string resourceRequestId) => new(SQS.Name, SQS.Icon, FifoName(sqs.Name, sqs.Fifo), new TenantSqsQueue
    {
        Name = FifoName(sqs.Name, sqs.Fifo),
        FifoQueue = sqs.Fifo,
        ContentBasedDeduplication = sqs.ContentBasedDeduplication,
        // Subscriptions = []   // TODO: handle subs

        // TODO: What about these?
        // ReceiveWaitTimeSeconds = 
        // sqs.VisibilityTimeout
        // sqs.ContentBasedDeduplication
        // sqs.DeduplicationScope
        // sqs.FifoThroughputLimit
        // sqs.DqlMaxReceiveCount
        // sqs.RedriveAllowPolicyByQueue
    })
    {
        ResourceRequestId = resourceRequestId
    };

    public static EntityResource<TenantSnsTopic> Map(CreateTenantSnsTopic sns, string resourceRequestId) => new(SNS.Name, SNS.Icon, FifoName(sns.Name, sns.Fifo), new TenantSnsTopic
    {
        Name = FifoName(sns.Name, sns.Fifo),
        FifoTopic = sns.Fifo,
        ContentBasedDeduplication = sns.ContentDeduplication
    })
    {
        ResourceRequestId = resourceRequestId
    };
    
    public static EntityResources FromCdpTenant(CdpTenant tenant)
    {
        return new EntityResources
        {
            S3Buckets = tenant.S3Buckets?.Select(Map).ToList() ?? [],
            SqsQueues = tenant.SqsQueues?.Select(Map).ToList() ?? [],
            SnsTopics = tenant.SnsTopics?.Select(Map).ToList() ?? [],
            SqlDatabase = tenant.SqlDatabase == null ? [] : [Map(tenant.SqlDatabase)],
            Dynamodb = tenant.Dynamodb?.Select(Map).ToList() ?? [],
            ApiGateways = tenant.ApiGateways?.Select(Map).ToList() ?? [],
            CognitoIdentityPool = tenant.CognitoIdentityPool == null ? [] : [Map(tenant.CognitoIdentityPool)],
            BedrockAi = tenant.BedrockAi?.Profiles?.Select(Map).ToList() ?? []
        };
    }

    public static EntityResources FromResourceRequestRecord(ResourceRequestRecord request, Entity entity, string env)
    {
        var resourceRequestId = request.Id.ToString()!;
        var name = entity.Name;

        return new EntityResources
        {
            S3Buckets = request.Resources?.S3Buckets?.FindAll(s3 => s3.Service == name).Select(s3 => Map(s3, resourceRequestId, env)).ToList() ?? [],
            SqsQueues = request.Resources?.SqsQueues?.FindAll(s3 => s3.Service == name).Select(sqs => Map(sqs, resourceRequestId)).ToList() ?? [],
            SnsTopics = request.Resources?.SnsTopics?.FindAll(s3 => s3.Service == name).Select(sns => Map(sns, resourceRequestId)).ToList() ?? []
        };
    }

    private static string FifoName(string name, bool isFifo)
    {
        return isFifo ? name + ".fifo" : name;
    }

    private static string ExpandBucketName(string name, string env)
    {
        return EntityResourceService.BucketNameForEnv(name, env);
    }
}

public static class EntityResourceCombiner {

    /*
     * Combine EntityResources based on Name. primary takes precedence over a match on secondary
     */
    public static EntityResources Combine(EntityResources primary, EntityResources secondary)
    {
        return new EntityResources
        {
            S3Buckets = primary.S3Buckets.Concat(Deduplicate(secondary.S3Buckets, primary.S3Buckets)).ToList() ?? [],
            SqsQueues = primary.SqsQueues.Concat(Deduplicate(secondary.SqsQueues, primary.SqsQueues)).ToList() ?? [],
            SnsTopics = primary.SnsTopics.Concat(Deduplicate(secondary.SnsTopics, primary.SnsTopics)).ToList() ?? [],
            
            SqlDatabase = primary.SqlDatabase,
            Dynamodb = primary.Dynamodb,
            ApiGateways = primary.ApiGateways,
            CognitoIdentityPool = primary.CognitoIdentityPool,
            BedrockAi = primary.BedrockAi
        };
    }

    private static List<EntityResource<T>> Deduplicate<T>(List<EntityResource<T>> items, List<EntityResource<T>> existing) {
        return items.FindAll(item => existing.Find(e => e.Name == item.Name) == null);
    }
}