using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;

namespace Defra.Cdp.Backend.Api.Tests.Services.Entities;

public class EntityResourceCombinerTests
{
    [Fact]
    public void FromCdpTenant_handles_null_resource_lists()
    {
        var primary = new EntityResources();
        var secondary = new EntityResources();

        var resources = EntityResourceCombiner.Combine(primary, secondary);

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
    public void FromCdpTenant_combines_s3_buckets()
    {
        var myBucket1 = new EntityResource<TenantS3Bucket>("type", "icon", "my-bucket-1", new TenantS3Bucket());
        var myBucket2 = new EntityResource<TenantS3Bucket>("type", "icon", "my-bucket-2", new TenantS3Bucket());
        var myBucket2Request = new EntityResource<TenantS3Bucket>("type", "icon", "my-bucket-2", new TenantS3Bucket())
        {
            ResourceRequestId = "123"
        };
        var myBucket3Request = new EntityResource<TenantS3Bucket>("type", "icon", "my-bucket-3", new TenantS3Bucket())
        {
            ResourceRequestId = "123"
        };

        var primary = new EntityResources()
        {
            S3Buckets = [
                myBucket1,
                myBucket2
            ]
        };
        var secondary = new EntityResources()
        {
            S3Buckets = [
                myBucket2Request,
                myBucket3Request
            ]
        };

        var resources = EntityResourceCombiner.Combine(primary, secondary);

        Assert.Equal(resources.S3Buckets, [myBucket1, myBucket2, myBucket3Request]);
    }

    [Fact]
    public void FromCdpTenant_combines_sqs_queues()
    {
        var myQueue1 = new EntityResource<TenantSqsQueue>("type", "icon", "my-Queue-1", new TenantSqsQueue());
        var myQueue2 = new EntityResource<TenantSqsQueue>("type", "icon", "my-Queue-2", new TenantSqsQueue());
        var myQueue2Request = new EntityResource<TenantSqsQueue>("type", "icon", "my-Queue-2", new TenantSqsQueue())
        {
            ResourceRequestId = "123"
        };
        var myQueue3Request = new EntityResource<TenantSqsQueue>("type", "icon", "my-Queue-3", new TenantSqsQueue())
        {
            ResourceRequestId = "123"
        };

        var primary = new EntityResources()
        {
            SqsQueues = [
                myQueue1,
                myQueue2
            ]
        };
        var secondary = new EntityResources()
        {
            SqsQueues = [
                myQueue2Request,
                myQueue3Request
            ]
        };

        var resources = EntityResourceCombiner.Combine(primary, secondary);

        Assert.Equal(resources.SqsQueues, [myQueue1, myQueue2, myQueue3Request]);
    }

    [Fact]
    public void FromCdpTenant_combines_sns_topics() {
        var myTopic1 = new EntityResource<TenantSnsTopic>("type", "icon", "my-Topic-1", new TenantSnsTopic());
        var myTopic2 = new EntityResource<TenantSnsTopic>("type", "icon", "my-Topic-2", new TenantSnsTopic());
        var myTopic2Request = new EntityResource<TenantSnsTopic>("type", "icon", "my-Topic-2", new TenantSnsTopic())
        {
            ResourceRequestId = "123"
        };
        var myTopic3Request = new EntityResource<TenantSnsTopic>("type", "icon", "my-Topic-3", new TenantSnsTopic())
        {
            ResourceRequestId = "123"
        };
        
        var primary = new EntityResources(){
            SnsTopics = [
                myTopic1,
                myTopic2
            ]
        };
        var secondary = new EntityResources(){
            SnsTopics = [
                myTopic2Request,
                myTopic3Request
            ]
        };

        var resources = EntityResourceCombiner.Combine(primary, secondary);

        Assert.Equal(resources.SnsTopics, [myTopic1, myTopic2, myTopic3Request]);
    }
}