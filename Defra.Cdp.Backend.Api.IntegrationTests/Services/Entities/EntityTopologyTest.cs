using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;

namespace Defra.Cdp.Backend.Api.IntegrationTests.Services.Entities;

public class EntityTopologyTest
{

    [Fact]
    public void Test_empty_topology()
    {
        var rootService = new TopologyService("foo", SubType.Backend, [], []);
        var resources = new EntityResources();
        
        var links = EntityTopologyService.LinkResources(rootService, resources, [], []);
        Assert.Single(links);
        Assert.Equal(links[0].Name, rootService.Name);
    }
    
    [Fact]
    public void Test_topology_with_S3()
    {
        var rootService = new TopologyService("foo", SubType.Backend, [], []);
        var resources = new EntityResources { S3Buckets = [ new EntityResource<TenantS3Bucket>("s3", "aws-s3", "foo-bucket", new TenantS3Bucket())]};
        
        var links = EntityTopologyService.LinkResources(rootService, resources, [], []);
        Assert.Single(links);
        Assert.Equal(links[0].Name, rootService.Name);
        Assert.Single(links[0].Resources);
        Assert.Equal("foo-bucket", links[0].Resources[0].Name);
    }

    [Fact]
    public void Test_topology_sqs_sns_no_external_links()
    {
        var rootService = new TopologyService("foo", SubType.Backend, [], []);
        var resources = new EntityResources
        {
            SqsQueues = [new EntityResource<TenantSqsQueue>("sqs", "aws-sqs", "foo-queue", new TenantSqsQueue { Name = "foo-queue", FifoQueue = false, Subscriptions = ["foo-topic"]})],
            SnsTopics = [new EntityResource<TenantSnsTopic>("sns", "aws-sns", "foo-topic", new TenantSnsTopic { Name = "foo-topic", FifoTopic = false })]
        };
        List<QueueSubscriptions> queueLookup = [ 
            new("foo", SubType.Backend, [], "foo-queue", "foo-topic"), 
            new("bar", SubType.Backend, [], "bar-queue", "bar-topic") 
        ];

        List<TopicOwner> topicLookup =
        [
            new("foo", SubType.Backend, [], "foo-topic"),
            new("bar", SubType.Backend, [], "bar-topic")
        ];
        
        var links = EntityTopologyService.LinkResources(rootService, resources, queueLookup, topicLookup);
        Assert.Single(links);
        Assert.Equal(links[0].Name, rootService.Name);
        Assert.Equal(2, links[0].Resources.Count);
        
        Assert.Equivalent(new TopologyResource("foo-topic", "sqs", "aws-sns", []), links[0].Resources[0]);
        Assert.Equivalent(new TopologyResource("foo-queue", "sqs", "aws-sqs", [ new TopologyResourceLink("foo", "sns", "foo-topic", "subscription") ]), links[0].Resources[1]);
    }
    
    [Fact]
    public void Test_topology_sqs_subscribed_to_another_services_topic()
    {
        var rootService = new TopologyService("foo", SubType.Backend, [], []);
        var resources = new EntityResources
        {
            SqsQueues = [new EntityResource<TenantSqsQueue>("sqs", "aws-sqs", "foo-queue", new TenantSqsQueue { Name = "foo-queue", FifoQueue = false, Subscriptions = ["bar-topic"]})],
        };
        
        List<QueueSubscriptions> queueLookup = [ 
            new("bar", SubType.Backend, [], "foo-queue", "bar-topic") 
        ];
        List<TopicOwner> topicLookup =
        [
            new("foo", SubType.Backend, [], "foo-topic"),
            new("bar", SubType.Backend, [], "bar-topic")
        ];
        
        var links = EntityTopologyService.LinkResources(rootService, resources, queueLookup, topicLookup);
        Assert.Equal(2, links.Count);
        Assert.Equal("foo", links[0].Name);
        Assert.Equal("bar", links[1].Name);
        Assert.Single(links[0].Resources);
        
        Assert.Equivalent(new TopologyResource("foo-queue", "sqs", "aws-sqs", [ new TopologyResourceLink("bar", "sns", "bar-topic", "subscription") ]), links[0].Resources[0]);
        Assert.Equivalent(new TopologyService("bar", SubType.Backend, [], [new TopologyResource("bar-topic", "sns", "aws-sns", [])]), links[1]);
    }
   
    
    [Fact]
    public void Test_topology_sns_subscribed_to_by_another_service()
    {
        var rootService = new TopologyService("foo", SubType.Backend, [], []);
        var resources = new EntityResources
        {
            SnsTopics = [new EntityResource<TenantSnsTopic>("sns", "aws-sns", "foo-topic", new TenantSnsTopic())],
        };
        
        List<QueueSubscriptions> queueLookup = [ 
            new("bar", SubType.Backend, [], "bar-queue", "foo-topic") 
        ];
        
        List<TopicOwner> topicLookup = [ 
            new("foo", SubType.Backend, [], "foo-topic") 
        ];

        
        var links = EntityTopologyService.LinkResources(rootService, resources, queueLookup, topicLookup);
        Assert.Equal(2, links.Count);
        Assert.Equal("foo", links[0].Name);
        Assert.Equal("bar", links[1].Name);
        Assert.Single(links[0].Resources);
        
        Assert.Equivalent(new TopologyService("foo", SubType.Backend, [], [new TopologyResource("foo-topic", "sns", "aws-sns", [])]), links[0]);
        Assert.Equivalent(new TopologyService("bar", SubType.Backend, [], [new TopologyResource("bar-queue", "sqs", "aws-sqs", [ new TopologyResourceLink("foo", "sns", "foo-topic", "subscription")])]), links[1]);
    }
}