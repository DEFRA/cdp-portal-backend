using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Entities;

public interface IEntityTopologyService
{
    Task<List<TopologyService>> ListTopologyOfEntity(string name, string environment,
        CancellationToken ct = default);

    Task<List<TopologyService>> ListTopologyOfResourceRequest(ResourceRequestRecord request, string name, string environment,
         CancellationToken ct = default);
}

public record QueueSubscriptions(string Service, SubType SubType, List<Team> Teams, string Queue, string Topic)
{
    public string? ResourceRequestId { get; set; } = null;
};

public record TopicOwner(string Service, SubType SubType, List<Team> Teams, string Topic)
{
    public string? ResourceRequestId { get; set; } = null;
};

public class EntityTopologyService(IMongoDbClientFactory mongoDbClientFactory) : IEntityTopologyService
{

    private async Task<List<QueueSubscriptions>> BuildQueueLookup(string environment, CancellationToken ct)
    {
        var collection = mongoDbClientFactory.GetCollection<Entity>("entities");

        var queuePath = $"$environments.{environment}.sqsQueues";
        var subsPath = $"$environments.{environment}.sqsQueues.subscriptions";
        var queueNamePath = $"$environments.{environment}.sqsQueues.name";

        var pipeline = new[]
        {
            new BsonDocument("$unwind", queuePath),
            new BsonDocument("$unwind", subsPath),
            new BsonDocument("$project", new BsonDocument
            {
                { "_id", 0 },
                { "service", "$name" },
                { "subType", "$subType" },
                { "teams", "$teams" },
                { "queue", queueNamePath },
                { "topic", subsPath }
            })
        };

        return await collection
            .Aggregate<QueueSubscriptions>(pipeline, cancellationToken: ct)
            .ToListAsync(ct);
    }

    private async Task<List<QueueSubscriptions>> BuildQueueLookupFromResourceRequest(ResourceRequestRecord request, string environment, CancellationToken ct)
    {
        var queueServices = request.Resources?.Subscriptions.Select(sub => sub.QueueService).ToList() ?? [];

        var entities = await mongoDbClientFactory.GetCollection<Entity>("entities").Find(e => queueServices.Contains(e.Name)).ToListAsync(ct);

        return request.Resources?.Subscriptions.Select(sub => {
            var entity = entities.Find(e => e.Name == sub.QueueService)!;
            return new QueueSubscriptions(sub.QueueService, entity.SubType ?? SubType.Backend, entity.Teams, sub.Queue, sub.Topic){
                ResourceRequestId = request.Id.ToString()
            };
        }).ToList() ?? [];
    }

    private async Task<List<TopicOwner>> BuildTopicLookup(string environment, CancellationToken ct)
    {
        var collection = mongoDbClientFactory.GetCollection<Entity>("entities");

        var topicPath = $"$environments.{environment}.snsTopics";
        var topicNamePath = $"$environments.{environment}.snsTopics.name";

        var pipeline = new[]
        {
            new BsonDocument("$unwind", topicPath),
            new BsonDocument("$project", new BsonDocument
            {
                { "_id", 0 },
                { "service", "$name" },
                { "subType", "$subType" },
                { "teams", "$teams" },
                { "topic", topicNamePath }
            })
        };

        return await collection
            .Aggregate<TopicOwner>(pipeline)
            .ToListAsync(ct);
    }

    private async Task<List<TopicOwner>> BuildTopicLookupFromResourceRequest(ResourceRequestRecord request, string environment, CancellationToken ct)
    {
        var topicServices = request.Resources?.Subscriptions.Select(sub => sub.TopicService).ToList() ?? [];

        var entities = await mongoDbClientFactory.GetCollection<Entity>("entities").Find(e => topicServices.Contains(e.Name)).ToListAsync(ct);

        return request.Resources?.Subscriptions.Select(sub => {
            var entity = entities.Find(e => e.Name == sub.TopicService)!;
            return new TopicOwner(sub.TopicService, entity.SubType ?? SubType.Backend, entity.Teams, sub.Topic){
                ResourceRequestId = request.Id.ToString()
            };
        }).ToList() ?? [];
    }

    public async Task<List<TopologyService>> ListTopologyOfEntity(string name, string environment,
        CancellationToken ct = default)
    {
        var entity = await mongoDbClientFactory.GetCollection<Entity>("entities").Find(e => e.Name == name).FirstOrDefaultAsync(ct);
        if (entity == null || !entity.Environments.ContainsKey(environment))
        {
            return [];
        }
        var queueTopicLookup = await BuildQueueLookup(environment, ct);
        var topicLookup = await BuildTopicLookup(environment, ct);
        var resources = EntityResourceMapper.FromCdpTenant(entity.Environments[environment]);
        var rootService = new TopologyService(entity.Name, entity.SubType, entity.Teams, []);

        return LinkResources(rootService, resources, queueTopicLookup, topicLookup);
    }

    public async Task<List<TopologyService>> ListTopologyOfResourceRequest(ResourceRequestRecord request, string name, string environment,
        CancellationToken ct = default)
    {
        var entity = await mongoDbClientFactory.GetCollection<Entity>("entities").Find(e => e.Name == name).FirstOrDefaultAsync(ct);
        if (entity == null || !entity.Environments.ContainsKey(environment))
        {
            return [];
        }

        var rootService = new TopologyService(entity.Name, entity.SubType, entity.Teams, []);
        var queueTopicLookup = await BuildQueueLookupFromResourceRequest(request, environment, ct);
        var topicLookup = await BuildTopicLookupFromResourceRequest(request, environment, ct);

        return LinkResources(rootService, EntityResourceMapper.FromResourceRequestRecord(request, entity, environment), queueTopicLookup, topicLookup);
    }

    public static List<TopologyService> LinkResources(TopologyService rootService, EntityResources resources, List<QueueSubscriptions> queueTopicLookup, List<TopicOwner> topicLookup)
    {

        var services = new Dictionary<string, TopologyService>
        {
            [rootService.Name] = rootService
        };


        // S3 Buckets
        foreach (var resource in resources.S3Buckets.Select(resourceS3Bucket => new TopologyResource(resourceS3Bucket.Name, resourceS3Bucket.Resource, resourceS3Bucket.Icon, [])
        {
            ResourceRequestId = resourceS3Bucket.ResourceRequestId
        }))
        {
            services[rootService.Name].Resources.Add(resource);
            // TODO: add shared access once we have the IAM data in the entity
        }

        // SNS Topics
        foreach (var topic in resources.SnsTopics.Select(snsTopic => new TopologyResource(snsTopic.Name, snsTopic.Resource, snsTopic.Icon, [])
        {
            ResourceRequestId = snsTopic.ResourceRequestId
        }))
        {
            services[rootService.Name].Resources.Add(topic);

            // Find any other services that subscribe to it
            var subscriptions = queueTopicLookup.Where(q => q.Topic == topic.Name);
            foreach (var sub in subscriptions)
            {
                if (sub.Service == rootService.Name) continue;

                services.TryAdd(sub.Service, new TopologyService(sub.Service, sub.SubType, sub.Teams, []));
                // Link back to root service's topic
                services[sub.Service].Resources.Add(
                    new TopologyResource(sub.Queue, EntityResourceMapper.SQS.Name, EntityResourceMapper.SQS.Icon, [new TopologyResourceLink(rootService.Name, sub.Topic, EntityResourceMapper.SNS.Name, "subscription")])
                    {
                        ResourceRequestId = sub.ResourceRequestId
                    }
                );
            }
        }

        // SQS Queues
        foreach (var queue in resources.SqsQueues)
        {
            var resource = new TopologyResource(queue.Name, queue.Resource, queue.Icon, [])
            {
                ResourceRequestId = queue.ResourceRequestId
            };

            foreach (var topicName in queue.Properties.Subscriptions)
            {

                var topicQueueIsSubscribedTo = topicLookup.Find(q => q.Topic == topicName);
                resource.Links?.Add(
                    new TopologyResourceLink(topicQueueIsSubscribedTo?.Service, topicName, EntityResourceMapper.SNS.Name, "subscription") {
                        ResourceRequestId = topicQueueIsSubscribedTo?.ResourceRequestId
                    }
                );

                // Add topics owned by services outside the current service
                if (topicQueueIsSubscribedTo == null || topicQueueIsSubscribedTo.Service == rootService.Name) continue;

                services.TryAdd(topicQueueIsSubscribedTo.Service, new TopologyService(topicQueueIsSubscribedTo.Service, topicQueueIsSubscribedTo.SubType, topicQueueIsSubscribedTo.Teams, []));
                services[topicQueueIsSubscribedTo.Service].Resources.Add(
                    new TopologyResource(topicName, EntityResourceMapper.SNS.Name, EntityResourceMapper.SNS.Icon, [])
                    {
                        ResourceRequestId = topicQueueIsSubscribedTo.ResourceRequestId
                    }
                );
            }

            services[rootService.Name].Resources.Add(resource);
        }

        return services.Values.OrderByDescending(s => s.Name == rootService.Name).ToList() ?? [];
    }
}
