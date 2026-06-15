using Defra.Cdp.Backend.Api.Services.Create.Models;

namespace Defra.Cdp.Backend.Api.Services.Create.Validators;

public class S3Validator
{

    public static async Task<List<string>> Validate(CreateTenantS3Bucket s3, IEntityResourceService entities, CancellationToken cancellationToken)
    {
        List<string> errors = [];
        
        // Check name is valid
        // Bucket names must be between 3 (min) and 63 (max) characters long. (minus -6 for hash suffix, - max 11 for env prefix)
        // TODO: regex the other rules
        if (s3.Name.Length > 46)
        {
            errors.Add($"S3 Bucket {s3.Name} name is too long (max 46 chars)");
        }
        
        // Check service exists
        var serviceExists = await entities.ServiceExists(s3.Service, cancellationToken);
        if (!serviceExists)
        {
            errors.Add($"S3 Bucket {s3.Name} is assigned to an unknown service: {s3.Service}");
        }
        
        var envs = CreateResourceEnvironments.ToCdpEnvironments(s3.Environments);
        if (envs.Length == 0)
        {
            errors.Add($"S3 Bucket {s3.Name} has an invalid or missing environment: {s3.Environments}");
        }
        
        // Check if the bucket already exists

        var existingOwner =
            await entities.BucketExists(s3.Name, CreateResourceEnvironments.ToCdpEnvironments(s3.Environments), cancellationToken);
        if (existingOwner != null) {
            errors.Add($"S3 Bucket {s3.Name} already exists for service {existingOwner}");
        }
        return errors;
    }
}