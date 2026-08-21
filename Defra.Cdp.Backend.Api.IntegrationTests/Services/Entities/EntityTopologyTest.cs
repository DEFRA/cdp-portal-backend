using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;
using static Defra.Cdp.Backend.Api.Services.Entities.Model.EntityResourceMapper;

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
        var resources = new EntityResources { S3Buckets = [ new EntityResource<TenantS3Bucket>(S3.Name, S3.Icon, "foo-bucket", new TenantS3Bucket())]};
        
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
            SqsQueues = [new EntityResource<TenantSqsQueue>(SQS.Name, SQS.Icon, "foo-queue", new TenantSqsQueue { Name = "foo-queue", FifoQueue = false, Subscriptions = ["foo-topic"]})],
            SnsTopics = [new EntityResource<TenantSnsTopic>(SNS.Name, SNS.Icon, "foo-topic", new TenantSnsTopic { Name = "foo-topic", FifoTopic = false })]
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
        
        Assert.Equivalent(new TopologyResource("foo-topic", SNS.Name,  SNS.Icon, []), links[0].Resources[0]);
        Assert.Equivalent(new TopologyResource("foo-queue", SQS.Name, SQS.Icon, [ new TopologyResourceLink("foo", "foo-topic", SNS.Name, "subscription") ]), links[0].Resources[1]);
    }
    
    [Fact]
    public void Test_topology_sqs_subscribed_to_another_services_topic()
    {
        var rootService = new TopologyService("foo", SubType.Backend, [], []);
        var resources = new EntityResources
        {
            SqsQueues = [new EntityResource<TenantSqsQueue>(SQS.Name, SQS.Icon, "foo-queue", new TenantSqsQueue { Name = "foo-queue", FifoQueue = false, Subscriptions = ["bar-topic"]})],
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
        
        Assert.Equivalent(new TopologyResource("foo-queue", SQS.Name, SQS.Icon, [ new TopologyResourceLink("bar", "bar-topic", SNS.Name, "subscription") ]), links[0].Resources[0]);
        Assert.Equivalent(new TopologyService("bar", SubType.Backend, [], [new TopologyResource("bar-topic", SNS.Name, SNS.Icon, [])]), links[1]);
    }
   
    
    [Fact]
    public void Test_topology_sns_subscribed_to_by_another_service()
    {
        var rootService = new TopologyService("foo", SubType.Backend, [], []);
        var resources = new EntityResources
        {
            SnsTopics = [new EntityResource<TenantSnsTopic>(SNS.Name, SNS.Icon, "foo-topic", new TenantSnsTopic())],
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
        
        Assert.Equivalent(new TopologyService("foo", SubType.Backend, [], [new TopologyResource("foo-topic", SNS.Name, SNS.Icon, [])]), links[0]);
        Assert.Equivalent(new TopologyService("bar", SubType.Backend, [], [new TopologyResource("bar-queue", SQS.Name, SQS.Icon, [ new TopologyResourceLink("foo", "foo-topic", SNS.Name, "subscription")])]), links[1]);
    }

    [Fact]
    public void Test_queue_subscriptions_are_built_from_sns_topic_subscribers()
    {
        var topics = new List<EntityResource<TenantSnsTopic>>
        {
            new(SNS.Name, SNS.Icon, "foo-topic", new TenantSnsTopic
            {
                Name = "foo-topic",
                Subscribers =
                [
                    new TenantSnsSubscriber { QueueName = "bar-queue", QueueOwner = "bar" }
                ]
            })
        };

        var owners = new Dictionary<string, (SubType SubType, List<Team> Teams)>
        {
            ["bar"] = (SubType.Backend, [])
        };

        var output = EntityTopologyService.QueueSubscriptionsFromTopicSubscribers(topics, owners);

        Assert.Single(output);
        Assert.Equal("bar", output[0].Service);
        Assert.Equal("bar-queue", output[0].Queue);
        Assert.Equal("foo-topic", output[0].Topic);
        Assert.Equal(SubType.Backend, output[0].SubType);
    }
    
    [Fact]
    public void Test_queue_subscriptions_fan_out_across_multiple_topics_and_subscribers()
    {
        var topics = new List<EntityResource<TenantSnsTopic>>
        {
            new(SNS.Name, SNS.Icon, "topic-one", new TenantSnsTopic
            {
                Name = "topic-one",
                Subscribers =
                [
                    new TenantSnsSubscriber { QueueName = "queue-a", QueueOwner = "service-a" },
                    new TenantSnsSubscriber { QueueName = "queue-b", QueueOwner = "service-b" }
                ]
            }),
            new(SNS.Name, SNS.Icon, "topic-two", new TenantSnsTopic
            {
                Name = "topic-two",
                Subscribers =
                [
                    new TenantSnsSubscriber { QueueName = "queue-c", QueueOwner = "service-a" }
                ]
            })
        };

        var owners = new Dictionary<string, (SubType SubType, List<Team> Teams)>
        {
            ["service-a"] = (SubType.Backend, [new Team { TeamId = "team-a", Name = "Team A" }]),
            ["service-b"] = (SubType.Frontend, [new Team { TeamId = "team-b", Name = "Team B" }])
        };

        var output = EntityTopologyService.QueueSubscriptionsFromTopicSubscribers(topics, owners);

        Assert.Equal(3, output.Count);

        Assert.Contains(output, s => s.Topic == "topic-one" && s.Service == "service-a" && s.Queue == "queue-a" && s.SubType == SubType.Backend);
        Assert.Contains(output, s => s.Topic == "topic-one" && s.Service == "service-b" && s.Queue == "queue-b" && s.SubType == SubType.Frontend);
        Assert.Contains(output, s => s.Topic == "topic-two" && s.Service == "service-a" && s.Queue == "queue-c" && s.SubType == SubType.Backend);
    }
}