using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Create.Validators;

public class SnsValidator : ICreateResourceValidator<CreateTenantSnsTopic>
{
    public async Task<List<string>> Validate(CreateTenantSnsTopic sns, ResourceValidatorContext ctx, CancellationToken cancellationToken)
    {
        List<string> errors = [];
        var entities = ctx.EntitiesCollection;

        // Check name length
        if (sns.Name.Length > 75)
        {
            errors.Add($"SNS Topic {sns.Name} name too long (>75 chars)");
        }
        
        // Check service exists
        var service = await entities.Find(e => e.Name == sns.Service).Project(e => e.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (service == null)
        {
            errors.Add($"SNS Topic {sns.Name} is assigned to an unknown service: {sns.Service}");
        }

        var envs = CreateResourceEnvironments.ToCdpEnvironments(sns.Environments);
        if (envs.Length == 0)
        {
            errors.Add($"SNS Topic {sns.Name} has an invalid or missing environment: {sns.Environments}");
        }
        
        // Check if it already exists
        var fb = new FilterDefinitionBuilder<Entity>();
        foreach (var env in envs)
        {
            var filter = fb.AnyEq(new StringFieldDefinition<Entity>($"environments.{env}.snsTopics.name"), sns.Name);
            var owner = await entities.Find(filter).Project(e => e.Name).FirstOrDefaultAsync(cancellationToken);
            if (owner == null) continue;
            errors.Add($"SNS Topic {sns.Name} already exists for service {owner}");
            break;
        }

        return errors;
    }
}