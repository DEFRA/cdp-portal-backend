using Amazon.S3;
using Amazon.S3.Model;
using Defra.Cdp.Backend.Api.Services.BucketManagement.Models;

namespace Defra.Cdp.Backend.Api.Services.BucketManagement;

public interface IBucketManagementService
{
    Task<List<BucketResource>?> ListBucketResources(string bucket, string basePath, string path, CancellationToken cancellationToken);
    Task<BucketResourceUrl?> GetBucketResourceUrl(string bucket, string basePath, string path, CancellationToken cancellationToken);
    Task<BucketResourceUrl?> GetBucketResourcePostUrl(string bucket, string basePath, string path, CancellationToken cancellationToken);
    Task<BucketResourceUrl?> GetBucketResourcePutUrl(string bucket, string basePath, string path, CancellationToken cancellationToken);
}

public class BucketManagementService(IAmazonS3 s3) : IBucketManagementService
{
    const int PRE_SIGNED_URL_TTL_SECONDS = 10;
    
    public async Task<List<BucketResource>?> ListBucketResources(string bucket, string basePath, string path, CancellationToken cancellationToken)
    {
        var fullPath = $"{basePath}{path}";
        
        var request = new ListObjectsV2Request{
            BucketName = bucket,
            Prefix = fullPath
        };

        List<BucketResource> objects = [];
        ListObjectsV2Response response;

        do
        {
            response = await s3.ListObjectsV2Async(request, cancellationToken);

            if (response.S3Objects == null)
            {
                return null;
            }

            foreach (var s3Object in response.S3Objects)
            {
                var isFolder = s3Object.Key.EndsWith('/');

                objects.Add(new BucketResource
                {
                    Name = s3Object.Key,
                    LastModified = s3Object.LastModified ?? DateTime.Now,
                    Size = s3Object.Size ?? 0,
                    // Path = s3Object.Key
                    IsFolder = isFolder
                });
            }

            request.ContinuationToken = response.NextContinuationToken; // TODO: handle using pagination
        }
        while (response.IsTruncated ?? false);

        return objects;
    }

    public async Task<BucketResourceUrl?> GetBucketResourceUrl(string bucket, string basePath, string path, CancellationToken cancellationToken)
    {
        var fullPath = $"{basePath}{path}";

        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = fullPath,
            Expires = DateTime.UtcNow.AddSeconds(PRE_SIGNED_URL_TTL_SECONDS),
            Verb = HttpVerb.GET
        };

        var url = await s3.GetPreSignedURLAsync(request);

        return new BucketResourceUrl
        {
            Method = "GET",
            Url = url
        };
    }

    public async Task<BucketResourceUrl?> GetBucketResourcePostUrl(string bucket, string basePath, string path, CancellationToken cancellationToken)
    {
        var fullPath = $"{basePath}{path}";

        var request = new CreatePresignedPostRequest
        {
            BucketName = bucket,
            Key = fullPath,
            Expires = DateTime.UtcNow.AddSeconds(PRE_SIGNED_URL_TTL_SECONDS)
        };

        var response = await s3.CreatePresignedPostAsync(request);

        return new BucketResourceUrl
        {
            Method = "POST",
            Url = response.Url
        };
    }

    public async Task<BucketResourceUrl?> GetBucketResourcePutUrl(string bucket, string basePath, string path, CancellationToken cancellationToken)
    {
        var fullPath = $"{basePath}{path}";

        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = fullPath,
            Expires = DateTime.UtcNow.AddSeconds(PRE_SIGNED_URL_TTL_SECONDS),
            Verb = HttpVerb.PUT
        };

        var url = await s3.GetPreSignedURLAsync(request);

        return new BucketResourceUrl
        {
            Method = "PUT",
            Url = url
        };
    }
}