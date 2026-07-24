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
    public void FromCdpTenant_combines_s3_buckets() {
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
        
        var primary = new EntityResources(){
            S3Buckets = [
                myBucket1,
                myBucket2
            ]
        };
        var secondary = new EntityResources(){
            S3Buckets = [
                myBucket2Request,
                myBucket3Request
            ]
        };

        var resources = EntityResourceCombiner.Combine(primary, secondary);

        Assert.Equal(resources.S3Buckets, [myBucket1, myBucket2, myBucket3Request]);
        
        Assert.Empty(resources.SqsQueues);
        Assert.Empty(resources.SnsTopics);
        Assert.Empty(resources.SqlDatabase);
        Assert.Empty(resources.Dynamodb);
        Assert.Empty(resources.ApiGateways);
        Assert.Empty(resources.CognitoIdentityPool);
        Assert.Empty(resources.BedrockAi);
    }
}