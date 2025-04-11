using Amazon.S3;
using Amazon.S3.Model;

namespace Defra.Cdp.Backend.Api.Services.Migrations;

public interface IAvailableMigrations
{
    public Task<List<string>> FindMigrationsForService(string service, CancellationToken ct);
}

public class AvailableMigrations(IAmazonS3 client, IConfiguration configuration) : IAvailableMigrations
{
    private readonly string? _bucketName = configuration.GetValue<string>("MigrationsBucket");
    
    public async Task<List<string>> FindMigrationsForService(string service, CancellationToken ct)
    {
        if (_bucketName == null)
        {
            throw new Exception("Config error: MigrationsBucket has not been set");
        }
        
        var prefix = service + "/";
        var request = new ListObjectsV2Request
        {
            BucketName = _bucketName,
            Prefix = prefix,
            Delimiter = "/"
            
        };

        List<string> migrations = []; 
        ListObjectsV2Response response;
        do
        {
            response = await client.ListObjectsV2Async(request, ct);
            migrations.AddRange(response.CommonPrefixes.Select(p => p.Replace(prefix, "").TrimEnd('/')));
            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated);
       
        return migrations;
    }

}

