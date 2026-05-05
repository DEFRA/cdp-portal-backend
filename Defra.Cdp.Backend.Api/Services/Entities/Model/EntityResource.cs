using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;

namespace Defra.Cdp.Backend.Api.Services.Entities.Model;

public record EntityResourceType(string Name, string Icon);


// Available icons https://icones.js.org/collection/logos
public record EntityResource<T>(string Resource, string Icon, string Name, T Properties);

public record EntityResources
{
    [JsonPropertyName("s3_buckets")] public List<EntityResource<TenantS3Bucket>> S3Buckets { get; set; } = [];
    [JsonPropertyName("sqs_queues")] public List<EntityResource<TenantSqsQueue>> SqsQueues { get; set; } = [];
    [JsonPropertyName("sns_topics")] public List<EntityResource<TenantSnsTopic>> SnsTopics { get; set; } = [];
    [JsonPropertyName("sql_database")] public List<EntityResource<TenantSqlDatabase>> SqlDatabase { get; set; } = [];
    [JsonPropertyName("dynamodb")] public List<EntityResource<TenantDynamoDB>> Dynamodb { get; set; } = [];
    [JsonPropertyName("api_gateway")] public List<EntityResource<TenantApiGateway>> ApiGateway { get; set; } = [];
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

    public static EntityResources FromCdpTenant(CdpTenant tenant)
    {
        return new EntityResources
        {
            S3Buckets = tenant.S3Buckets.Select(Map).ToList(),
            SqsQueues = tenant.SqsQueues.Select(Map).ToList(),
            SnsTopics = tenant.SnsTopics.Select(Map).ToList(),
            SqlDatabase = tenant.SqlDatabase == null ? [] : [Map(tenant.SqlDatabase)],
            Dynamodb = tenant.Dynamodb.Select(Map).ToList(),
            ApiGateway = tenant.ApiGateway == null ? [] : [Map(tenant.ApiGateway)],
            CognitoIdentityPool = tenant.CognitoIdentityPool == null ? [] : [Map(tenant.CognitoIdentityPool)],
            BedrockAi = tenant.BedrockAi?.Profiles == null ? [] : tenant.BedrockAi.Profiles.Select(Map).ToList()
        };
    }
}