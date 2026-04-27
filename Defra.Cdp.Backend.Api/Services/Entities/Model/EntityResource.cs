using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;

namespace Defra.Cdp.Backend.Api.Services.Entities.Model;

public record EntityResource<T>(string Resource, string Icon, string Name, T Properties)
{
    public string Resource { get; init; } = Resource;
    public string Icon { get; init; } = Icon;
    public string Name { get; init; } = Name;
    public T Properties { get; init; } = Properties;
}

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
    public static EntityResource<TenantS3Bucket> Map(TenantS3Bucket s3) => new("s3", "aws-s3", s3.BucketName, s3);
    public static EntityResource<TenantSqsQueue> Map(TenantSqsQueue sqs) => new("sqs", "aws-sqs", sqs.Name, sqs);
    public static EntityResource<TenantSnsTopic> Map(TenantSnsTopic sns) => new("sns", "aws-sns", sns.Name + (sns.FifoTopic ? ".fifo" : ""), sns);
    public static EntityResource<TenantDynamoDB> Map(TenantDynamoDB d) => new("dynamodb", "aws-dynamo", d.Name, d);
    public static EntityResource<TenantApiGateway> Map(TenantApiGateway api) => new("api", "aws-apigateway", api.Name, api);
    public static EntityResource<TenantSqlDatabase> Map(TenantSqlDatabase sql) => new("sql", "aws-rds", sql.Name, sql);
    public static EntityResource<TenantCognitoIdentityPool> Map(TenantCognitoIdentityPool cog) => new("cognito", "aws-cognito", cog.IdentityPoolName, cog);
    public static EntityResource<CdpBedrockProfile> Map(CdpBedrockProfile ai) => new("bedrock", "aws-bedrock", ai.Name, ai);

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