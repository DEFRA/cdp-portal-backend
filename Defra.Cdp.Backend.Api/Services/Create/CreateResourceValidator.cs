using System.Security.Cryptography;
using System.Text;
using Defra.Cdp.Backend.Api.Mongo;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Create.Validators;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Create;

interface ICreateResourceValidator<in T>
{
    Task<List<string>> Validate(T resource, ResourceValidatorContext ctx, CancellationToken cancellationToken);
}

public class ResourceValidatorContext
{
    public required CreateTenantResourceRequest OriginalRequest { get; init; }
    public required IMongoCollection<Entity> EntitiesCollection { get; init; }
}

public interface ICreateResourceValidator
{
    Task<List<string>> Validate(CreateTenantResourceRequest request, CancellationToken cancellationToken);
}

public class CreateResourceValidator(IMongoDbClientFactory mongoDbClientFactory) : ICreateResourceValidator
{
    
    public async Task<List<string>> Validate(CreateTenantResourceRequest request, CancellationToken cancellationToken)
    {
        var s3Validator = new S3Validator();
        var snsValidator = new SnsValidator();
        var sqsValidator = new SqsValidator();
        var subValidator = new SubscriptionValidator();
        
        var ctx = new ResourceValidatorContext
        {
            EntitiesCollection = mongoDbClientFactory.GetCollection<Entity>("entities"), OriginalRequest = request
        };

        List<string> errors = [];
        
        foreach (var s3 in request.S3Buckets)
        {
            errors.AddRange(await s3Validator.Validate(s3, ctx, cancellationToken));
        }
        
        foreach (var sns in request.SnsTopics)
        {
            errors.AddRange(await snsValidator.Validate(sns, ctx, cancellationToken));
        }

        foreach (var sqs in request.SqsQueues)
        {
            errors.AddRange(await sqsValidator.Validate(sqs, ctx, cancellationToken));
        }

        foreach (var sub in request.Subscriptions)
        {
            errors.AddRange(await subValidator.Validate(sub, ctx, cancellationToken));
        }
        
        return errors;
    }

}