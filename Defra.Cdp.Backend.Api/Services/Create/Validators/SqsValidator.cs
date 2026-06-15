using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Create.Validators;

public class SqsValidator : ICreateResourceValidator<CreateTenantSqsQueue>
{
    public async Task<List<string>> Validate(CreateTenantSqsQueue sqs, ResourceValidatorContext ctx, CancellationToken cancellationToken)
    {
        List<string> errors = [];
        var entities = ctx.EntitiesCollection;

        // Check name length
        if (sqs.Name.Length > 256)
        {
            errors.Add($"SQS Queue {sqs.Name} name too long (>256 chars)");
        }
        
        // Check service exists
        var service = await entities.Find(e => e.Name == sqs.Service).Project(e => e.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (service == null)
        {
            errors.Add($"SQS Queue {sqs.Name} is assigned to an unknown service: {sqs.Service}");
        }
        
        var envs = CreateResourceEnvironments.ToCdpEnvironments(sqs.Environments);
        if (envs.Length == 0)
        {
            errors.Add($"SQS Queue {sqs.Name} has an invalid or missing environment: {sqs.Environments}");
        }
        
        // Check if it already exists
        var fb = new FilterDefinitionBuilder<Entity>();
        foreach (var env in envs)
        {
            var filter = fb.AnyEq(new StringFieldDefinition<Entity>($"environments.{env}.sqsQueues.name"), sqs.Name);
            var owner = await entities.Find(filter).Project(e => e.Name).FirstOrDefaultAsync(cancellationToken);
            if (owner == null) continue;
            errors.Add($"SQS Queue {sqs.Name} already exists for service {owner}");
            break;
        }

        return errors;
    }
}