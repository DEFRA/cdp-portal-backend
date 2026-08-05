using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;
using MongoDB.Bson;

namespace Defra.Cdp.Backend.Api.Tests.Services.Entities;

public class EntityResourceMapperTests
{
    [Fact]
    public void FromCdpTenant_handles_null_resource_lists()
    {
        var tenant = new CdpTenant();

        var resources = EntityResourceMapper.FromCdpTenant(tenant);

        Assert.Empty(resources.S3Buckets);
        Assert.Empty(resources.SqsQueues);
        Assert.Empty(resources.SnsTopics);
        Assert.Empty(resources.SqlDatabase);
        Assert.Empty(resources.Dynamodb);
        Assert.Empty(resources.ApiGateways);
        Assert.Empty(resources.CognitoIdentityPool);
        Assert.Empty(resources.BedrockAi);
    }


    [Fact]
    public void FromResourceRequestRecord_handles_null_resource_lists()
    {
        var request = new ResourceRequestRecord() {
            Id = new ObjectId("6a3d0c67158671fdb9c13561")
        };
        var entity = new Entity
        {
            Name = "test-service"
        };

        var resources = EntityResourceMapper.FromResourceRequestRecord(request, entity, "dev");

        Assert.Empty(resources.S3Buckets);
        Assert.Empty(resources.SqsQueues);
        Assert.Empty(resources.SnsTopics);
        Assert.Empty(resources.SqlDatabase);
        Assert.Empty(resources.Dynamodb);
        Assert.Empty(resources.ApiGateways);
        Assert.Empty(resources.CognitoIdentityPool);
        Assert.Empty(resources.BedrockAi);
    } 

    [Fact]
    public void FromResourceRequestRecord_handles_subscription_request()
    {
        var entity = new Entity {
            Name = "test-service"
        };
        
        var request = new ResourceRequestRecord() {
            Id = new ObjectId("6a3d0c67158671fdb9c13561"),
            Entities = ["test-service"],
            Resources = new CreateTenantResourceRequest() {
                Subscriptions = [new CreateTenantSubscription() {
                    QueueService = "test-service",
                    Queue = "test-queue",
                    TopicService = "test-service",
                    Topic = "test-topic",
                    Environments = "platform"
                }]
            }
        };

        var resources = EntityResourceMapper.FromResourceRequestRecord(request, entity, "dev");

        Assert.Empty(resources.S3Buckets);
        Assert.Empty(resources.SnsTopics);
        Assert.Empty(resources.SqlDatabase);
        Assert.Empty(resources.Dynamodb);
        Assert.Empty(resources.ApiGateways);
        Assert.Empty(resources.CognitoIdentityPool);
        Assert.Empty(resources.BedrockAi);

        var expected = new List<EntityResource<TenantSqsQueue>>([
            new EntityResource<TenantSqsQueue>("sqs", "aws-sqs", "test-queue", new TenantSqsQueue{
                Name = "test-queue",
                Subscriptions = ["test-topic"]
            }) {
                ResourceRequestId = "6a3d0c67158671fdb9c13561"
            }
        ]);
        
        Assert.Equivalent(resources.SqsQueues, expected, true);
    } 
}