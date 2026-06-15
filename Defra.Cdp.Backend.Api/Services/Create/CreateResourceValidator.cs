using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Create.Validators;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Utils;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Create;

public interface ICreateResourceValidator
{
    Task<List<string>> Validate(CreateTenantResourceRequest request, CancellationToken cancellationToken);
}

public interface IEntityResourceService
{
    Task<bool> ServiceExists(string name, CancellationToken cancellationToken);
    Task<bool> ServiceExists(List<string> names, CancellationToken cancellationToken);
    
    // TODO: maybe improve the interface to use something better than string? 
    Task<string?> BucketExists(string name, string[] environments, CancellationToken cancellationToken);
    Task<string?> TopicExists(string name, string[] environments, CancellationToken cancellationToken);
    Task<string?> QueueExists(string name, string[] environments, CancellationToken cancellationToken);
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
    
    private static string BucketNameForEnv(string name, string env)
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

public class CreateResourceValidator(IEntityResourceService ers) : ICreateResourceValidator
{
    
    public async Task<List<string>> Validate(CreateTenantResourceRequest request, CancellationToken cancellationToken)
    {
        List<string> errors = [];
        
        foreach (var s3 in request.S3Buckets)
        {
            errors.AddRange(await S3Validator.Validate(s3, ers, cancellationToken));
        }
        
        foreach (var sns in request.SnsTopics)
        {
            errors.AddRange(await SnsValidator.Validate(sns, ers, cancellationToken));
        }

        foreach (var sqs in request.SqsQueues)
        {
            errors.AddRange(await SqsValidator.Validate(sqs, ers, cancellationToken));
        }

        foreach (var sub in request.Subscriptions)
        {
            errors.AddRange(await SubscriptionValidator.Validate(sub, ers, request, cancellationToken));
        }
        
        return errors;
    }

}