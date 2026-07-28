using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Utils;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Create;

public interface IEntityResourceService
{
    Task<bool> ServiceExists(string name, CancellationToken cancellationToken);
    Task<bool> ServiceExists(List<string> names, CancellationToken cancellationToken);
    
    // TODO: maybe improve the interface to use something better than string? 
    Task<string?> BucketExists(string name, string[] environments, CancellationToken cancellationToken);
    Task<string?> TopicExists(string name, string[] environments, CancellationToken cancellationToken);
    Task<string?> QueueExists(string name, string[] environments, CancellationToken cancellationToken);
}

public record ResourceExists
{
    public required string Name { get; init; }
    public required string Owner { get; init; }
    public string? Environment { get; init; }
}

public class EntityResourceService(IMongoDbClientFactory connectionFactory) : IEntityResourceService
{
    private readonly IMongoCollection<Entity> _collection = connectionFactory.GetCollection<Entity>("entities");

    public async Task<bool> ServiceExists(string name, CancellationToken cancellationToken)
    {
        var count = await _collection.CountDocumentsAsync(f => f.Name == name, new CountOptions(), cancellationToken);
        return count > 0;
    }
    
    public async Task<bool> ServiceExists(List<string> names, CancellationToken cancellationToken)
    {
        var fb = new FilterDefinitionBuilder<Entity>();
        var count = await _collection.CountDocumentsAsync(fb.In(e => e.Name, names), new CountOptions(), cancellationToken);
        return count > 0;
    }

    public async Task<string?> BucketExists(string name, string[] environments, CancellationToken cancellationToken)
    {
        if (environments.Length == 0) return null;
        var fb = new FilterDefinitionBuilder<Entity>();
        var filter = fb.Or(
            environments
                .Select(env => fb.AnyEq(new StringFieldDefinition<Entity>($"environments.{env}.s3Buckets.bucketName"),
                    BucketNameForEnv(name, env)))
        );
        
        return await _collection.Find(filter).Project(e => e.Name).FirstOrDefaultAsync(cancellationToken);
    }
    
    public async Task<string?> TopicExists(string name, string[] environments, CancellationToken cancellationToken)
    {
        if (environments.Length == 0) return null;
        var fb = new FilterDefinitionBuilder<Entity>();
        var filter = fb.Or(
            environments
                .Select(env => fb.AnyEq(new StringFieldDefinition<Entity>($"environments.{env}.snsTopics.name"),
                    name))
        );
        
        return await _collection.Find(filter).Project(e => e.Name).FirstOrDefaultAsync(cancellationToken);
    }
    
    public async Task<string?> QueueExists(string name, string[] environments, CancellationToken cancellationToken)
    {
        if (environments.Length == 0) return null;
        
        var fb = new FilterDefinitionBuilder<Entity>();
        var filter = fb.Or(
            environments
                .Select(env => fb.AnyEq(new StringFieldDefinition<Entity>($"environments.{env}.sqsQueues.name"),
                    name))
        );
        
        return await _collection.Find(filter).Project(e => e.Name).FirstOrDefaultAsync(cancellationToken);
    }
    
    public static string BucketNameForEnv(string name, string env)
    {
        // The first 5 chars of the md5 of the account id
        var envHash = env switch
        {
            CdpEnvironments.InfraDev => "7df0c",
            CdpEnvironments.Management => "8dfff",
            CdpEnvironments.Dev => "c63f2",
            CdpEnvironments.Test => "6bf3a",
            CdpEnvironments.PerfTest => "05244",
            CdpEnvironments.ExtTest => "8ec5c",
            CdpEnvironments.Prod => "75ee2",
            _ => ""
        };

        return $"{env}-{name}-{envHash}";
    }
}