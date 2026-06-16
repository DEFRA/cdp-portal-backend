using Defra.Cdp.Backend.Api.Services.Create.Models;

namespace Defra.Cdp.Backend.Api.Services.Create.Validators;

public static class SqsValidator
{
    public static async Task<List<string>> Validate(CreateTenantSqsQueue sqs, IEntityResourceService entities, CancellationToken cancellationToken)
    {
        List<string> errors = [];

        // Check name length
        if (sqs.Name.Length > 256)
        {
            errors.Add($"SQS Queue {sqs.Name} name too long (>256 chars)");
        }
        
        // Check service exists
        var service = await entities.ServiceExists(sqs.Service, cancellationToken);
        if (!service)
        {
            errors.Add($"SQS Queue {sqs.Name} is assigned to an unknown service: {sqs.Service}");
        }
        
        var envs = CreateResourceEnvironments.ToCdpEnvironments(sqs.Environments);
        if (envs.Length == 0)
        {
            errors.Add($"SQS Queue {sqs.Name} has an invalid or missing environment: {sqs.Environments}");
        }
        
        // Check if it already exists
        var owner = await entities.QueueExists(sqs.Name, envs, cancellationToken);
        if (owner != null) {
            errors.Add($"SQS Queue {sqs.Name} already exists for service {owner}");
        }

        return errors;
    }
}