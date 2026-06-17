using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Create.Validators;

namespace Defra.Cdp.Backend.Api.Services.Create;

public interface ICreateResourceValidator
{
    Task<List<string>> Validate(CreateTenantResourceRequest request, CancellationToken cancellationToken);
}

public class CreateResourceValidator(IEntityResourceService ers) : ICreateResourceValidator
{
    
    public async Task<List<string>> Validate(CreateTenantResourceRequest request, CancellationToken cancellationToken)
    {
        List<string> errors = [];

        if (request.Count() == 0)
        {
            errors.Add("The request has no resources");
        }
        
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