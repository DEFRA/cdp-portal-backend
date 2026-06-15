using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Utils;
using MongoDB.Driver;

namespace Defra.Cdp.Backend.Api.Services.Create.Validators;

public class S3Validator : ICreateResourceValidator<CreateTenantS3Bucket>
{

    public async Task<List<string>> Validate(CreateTenantS3Bucket s3, ResourceValidatorContext ctx, CancellationToken cancellationToken)
    {
        List<string> errors = [];
        var entities = ctx.EntitiesCollection;

        // Check name is valid
        // Bucket names must be between 3 (min) and 63 (max) characters long. (minus -6 for hash suffix, - max 11 for env prefix)
        // TODO: regex the other rules
        if (s3.Name.Length > 46)
        {
            errors.Add($"S3 Bucket {s3.Name} name is too long (max 46 chars)");
        }
        
        // Check service exists
        var service = await entities
            .Find(e => e.Name == s3.Service)
            .Project(e => e.Name)
            .FirstOrDefaultAsync(cancellationToken);
        if (service == null)
        {
            errors.Add($"S3 Bucket {s3.Name} is assigned to an unknown service: {s3.Service}");
        }
        
        var envs = CreateResourceEnvironments.ToCdpEnvironments(s3.Environments);
        if (envs.Length == 0)
        {
            errors.Add($"S3 Bucket {s3.Name} has an invalid or missing environment: {s3.Environments}");
        }
        
        // Check if the bucket already exists
        var fb = new FilterDefinitionBuilder<Entity>();
        foreach (var env in envs)
        {
            // S3 bucket names are unique per environment but follow the pattern: {env}-{name}-{hash}
            var envBucketName = BucketNameForEnv(s3.Name, env);
            var filter = fb.AnyEq(new StringFieldDefinition<Entity>($"environments.{env}.s3Buckets.bucketName"), envBucketName);
            var owner = await entities.Find(filter).Project(e => e.Name).FirstOrDefaultAsync(cancellationToken);
            if (owner == null) continue;
            errors.Add($"S3 Bucket {s3.Name} already exists for service {owner}");
            break;
        }
        return errors;
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