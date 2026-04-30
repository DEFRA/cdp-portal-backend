using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using MongoDB.Bson;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Entities;

public interface IEntityRelationshipsService
{
    Task<List<TopologyService>> ListTopologyOfEntity(string name, string environment,
        CancellationToken ct = default);
}



public class EntityRelationshipsService(IMongoDbClientFactory mongoDbClientFactory) : IEntityRelationshipsService {

    private record QueueTopic(string Name, SubType SubType, List<Team> Teams, string Queue, string Topic);

    private async Task<List<QueueTopic>> BuildQueueTopicLookup(string environment, CancellationToken ct)
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
                { "name", "$name" },
                { "subType", "$subType" },
                { "teams", "$teams" },
                { "queue", queueNamePath },
                { "topic", subsPath }
            })
        };

        return await collection
            .Aggregate<QueueTopic>(pipeline)
            .ToListAsync(ct);
        
    }

    public async Task<List<TopologyService>> ListTopologyOfEntity(string name, string environment, CancellationToken ct = default)
    {
        // Get entity
        var entity = await mongoDbClientFactory.GetCollection<Entity>("entities").Find(e => e.Name == name).FirstOrDefaultAsync(ct);
        if (entity == null || !entity.Environments.ContainsKey(environment))
        {
            return [];
        }

        var services = new Dictionary<string, TopologyService>
        {
            [name] = new(name, entity.SubType, entity.Teams, [])
        };

        // get resources 
        var resources = EntityResourceMapper.FromCdpTenant(entity.Environments[environment]);
        var queueTopicLookup = await BuildQueueTopicLookup(environment, ct);
        
        // S3 Buckets
        foreach (var resource in resources.S3Buckets.Select(resourceS3Bucket => new TopologyResource(resourceS3Bucket.Name, resourceS3Bucket.Icon, [])))
        {
            services[name].Resources.Add(resource);
        }

        // SNS Topics
        foreach (var topic in resources.SnsTopics.Select(resourcesSnsTopic => new TopologyResource(resourcesSnsTopic.Name, resourcesSnsTopic.Icon, [])))
        {
            services[name].Resources.Add(topic);
            
            // Find any other services that subscribe to it
            var subscriptions = queueTopicLookup.Where(q => q.Topic == topic.Name);
            foreach (var sub in subscriptions)
            {
                if(sub.Name == name) continue;

                services.TryAdd(sub.Name, new TopologyService(sub.Name, sub.SubType, sub.Teams, []));
                services[sub.Name].Resources.Add(
                    new TopologyResource(sub.Queue, "aws-sqs", [ new TopologyResourceLink(name, sub.Topic, "subscription") ]));
            }
        }
        
        // SQS Queues
        foreach (var queue in resources.SqsQueues)
        {
            var resource = new TopologyResource(queue.Name, queue.Icon, []);
            foreach (var topicName in queue.Properties.Subscriptions)
            {

                var ownerOfTopic = queueTopicLookup.Find(q => q.Topic == topicName);
                resource.Links?.Add(new TopologyResourceLink(ownerOfTopic?.Name, topicName, "subscription"));
                
                // Add topics owned by services outside the current service
                if (ownerOfTopic == null || ownerOfTopic.Name == name) continue;
               
                services.TryAdd(ownerOfTopic.Name, new TopologyService(ownerOfTopic.Name, ownerOfTopic.SubType, ownerOfTopic.Teams, []));
                services[ownerOfTopic.Name].Resources.Add(new TopologyResource(topicName, "aws-sns", []));
            }

            services[name].Resources.Add(resource);
        }

        return services.Values.OrderByDescending(s => s.Name == name).ToList();
    }
}
