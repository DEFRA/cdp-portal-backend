using Defra.Cdp.Backend.Api.Services.Create.Models;

namespace Defra.Cdp.Backend.Api.Services.Create.Validators;

public class SnsValidator
{
    public static async Task<List<string>> Validate(CreateTenantSnsTopic sns, IEntityResourceService entities, CancellationToken cancellationToken)
    {
        List<string> errors = [];

        // Check name length
        if (sns.Name.Length > 75)
        {
            errors.Add($"SNS Topic {sns.Name} name too long (>75 chars)");
        }
        
        // Check service exists
        var service = await entities.ServiceExists(sns.Service, cancellationToken);
        if (!service)
        {
            errors.Add($"SNS Topic {sns.Name} is assigned to an unknown service: {sns.Service}");
        }

        var envs = CreateResourceEnvironments.ToCdpEnvironments(sns.Environments);
        if (envs.Length == 0)
        {
            errors.Add($"SNS Topic {sns.Name} has an invalid or missing environment: {sns.Environments}");
        }
        
        // Check if it already exists
        var owner = await entities.TopicExists(sns.Name, envs, cancellationToken);
        if (owner != null)
        {
            errors.Add($"SNS Topic {sns.Name} already exists for service {owner}");
        }
    
        return errors;
    }
}