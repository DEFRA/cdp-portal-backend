using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;

namespace Defra.Cdp.Backend.Api.Tests.Services.Entities;

public class EntityResourceMapperTests
{
    [Fact]
    public void Combine_handles_null_resource_lists()
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
        var request = new ResourceRequestRecord();
        var entity = new Entity{
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
    
}