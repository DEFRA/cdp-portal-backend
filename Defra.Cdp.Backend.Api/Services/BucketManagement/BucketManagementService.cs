using Amazon.S3;
using Amazon.S3.Model;
using Defra.Cdp.Backend.Api.Services.BucketManagement.Models;

namespace Defra.Cdp.Backend.Api.Services.BucketManagement;

public interface IBucketManagementService
{
    Task<List<BucketResource>> GetBucketResources(String bucket, String path, CancellationToken cancellationToken);
}

public class BucketManagementService(IAmazonS3 s3) : IBucketManagementService
{
    public async Task<List<BucketResource>> GetBucketResources(String bucket, String path, CancellationToken ct)
    {
        var request = new ListObjectsV2Request
        {
            BucketName = bucket,
            // Prefix = path
        };

        List<BucketResource> objects = [];
        ListObjectsV2Response response;
        do
        {
            response = await s3.ListObjectsV2Async(request, ct);

            foreach (var s3Object in response.S3Objects)
            {
                objects.Add(new BucketResource
                {
                    Name = s3Object.Key,
                    LastModified = s3Object.LastModified ?? DateTime.Now,
                    Size = s3Object.Size ?? 0
                    // Path = s3Object.Key
                });
            }

            request.ContinuationToken = response.NextContinuationToken; // TODO: handle using pagination
        }
        while (response.IsTruncated ?? false);

        return objects;
    }
}