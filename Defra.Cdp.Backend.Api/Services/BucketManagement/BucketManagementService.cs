using Amazon.S3;
using Amazon.S3.Model;
using Defra.Cdp.Backend.Api.Services.BucketManagement.Models;

namespace Defra.Cdp.Backend.Api.Services.BucketManagement;

/**
 *  Manage S3 Buckets where objects are treated like a Files and Folders in a filesystem
 */
public interface IBucketManagementService
{
    Task<List<BucketResource>?> ListBucketResources(string bucket, string basePath, string path, CancellationToken cancellationToken);
    Task<BucketResourceUrl?> GetBucketResourceUrl(string bucket, string basePath, string path, CancellationToken cancellationToken);
    Task<BucketResourceUrl> GetBucketResourcePostUrl(string bucket, string basePath, string path, CancellationToken cancellationToken);
    Task<BucketResourceUrl> GetBucketResourcePutUrl(string bucket, string basePath, string path, CancellationToken cancellationToken);
    Task<BucketResource> CreateEmptyFolder(string bucket, string basePath, string path, CancellationToken cancellationToken);
    Task<bool?> RenameBucketResource(string bucket, string basePath, string path, string newName, CancellationToken cancellationToken);
    // TODO:
    // Task<bool?> DeleteBucketResource(string bucket, string basePath, string path, CancellationToken cancellationToken);
    // Task<bool?> DeleteFolder(string bucket, string basePath, string path, CancellationToken cancellationToken);
}

public class BucketManagementService(IAmazonS3 s3):IBucketManagementService
{
    private const int PRE_SIGNED_URL_TTL_SECONDS = 10;

    public async Task<List<BucketResource>?> ListBucketResources(string bucket, string basePath, string path, CancellationToken cancellationToken)
    {
        var fullPath = getFullPath(basePath, path);

        var request = new ListObjectsV2Request
        {
            BucketName = bucket,
            Prefix = fullPath
        };

        var resources = new SortedDictionary<string, BucketResource>(new ResourceCompare());
        ListObjectsV2Response response;

        do
        {
            response = await s3.ListObjectsV2Async(request, cancellationToken);

            if (response.S3Objects == null)
            {
                return null; // Not Found
            }

            foreach (var s3Object in response.S3Objects)
            {
                var (relPath, name, _isFolder) = getObjectPathInfo(basePath, path, s3Object.Key);
                var groupedPath = path == "" ? relPath : removeFirst(relPath, path);
                var isCurrentFolder = groupedPath == "";
                var isGroupedFolder = groupedPath.Contains('/');
                var groupedFolderName = groupedPath.Split("/")[0];

                if (isCurrentFolder)
                {
                    continue;
                }

                if (isGroupedFolder)
                {
                    if (resources.TryGetValue($"{groupedFolderName}/", out var resource))
                    {
                        resource.Size += s3Object.Size ?? 0;
                        if (s3Object.LastModified > resource.ModifiedDate)
                        {
                            resource.ModifiedDate = s3Object.LastModified.Value;
                        }
                    }
                    else
                    {
                        resources.Add($"{groupedFolderName}/", new BucketResource
                        {
                            Name = groupedFolderName,
                            ModifiedDate = s3Object.LastModified ?? DateTime.Now,
                            Size = s3Object.Size ?? 0,
                            Path = $"{path}{groupedFolderName}/",
                            IsFolder = true
                        });
                    }

                }
                else
                {
                    resources.Add(name, new BucketResource
                    {
                        Name = name,
                        ModifiedDate = s3Object.LastModified ?? DateTime.Now,
                        Size = s3Object.Size ?? 0,
                        Path = relPath,
                        IsFolder = false
                    });
                }

            }

            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated ?? false);

        return [.. resources.Values];
    }

    public async Task<BucketResourceUrl?> GetBucketResourceUrl(string bucket, string basePath, string path, CancellationToken cancellationToken)
    {
        var fullPath = getFullPath(basePath, path);

        if (!await bucketResourceExists(bucket, fullPath, cancellationToken))
        {
            return null; // Not Found
        }

        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = fullPath,
            Expires = DateTime.UtcNow.AddSeconds(PRE_SIGNED_URL_TTL_SECONDS),
            Verb = HttpVerb.GET
            // TODO: ResponseContentDisposition: 'attachment'
        };

        var url = await s3.GetPreSignedURLAsync(request);

        return new BucketResourceUrl
        {
            Method = "GET",
            Url = url
        };
    }

    public async Task<BucketResourceUrl> GetBucketResourcePostUrl(string bucket, string basePath, string path, CancellationToken cancellationToken)
    {
        var fullPath = getFullPath(basePath, path);

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

    public async Task<BucketResourceUrl> GetBucketResourcePutUrl(string bucket, string basePath, string path, CancellationToken cancellationToken)
    {
        var fullPath = getFullPath(basePath, path);

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

    public async Task<BucketResource> CreateEmptyFolder(string bucket, string basePath, string path, CancellationToken cancellationToken)
    {
        var fullPath = getFullPath(basePath, path);

        if (fullPath.Last() != '/')
        {
            // TODO: error
        }

        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = fullPath
        };

        var response = await s3.PutObjectAsync(request, cancellationToken);

        var (relPath, name, isFolder) = getObjectPathInfo(basePath, path, fullPath);

        return new BucketResource
        {
            Name = name,
            ModifiedDate = DateTime.Now,
            Size = response.Size ?? 0,
            Path = relPath,
            IsFolder = isFolder
        };
    }

    public async Task<bool?> RenameBucketResource(string bucket, string basePath, string path, string newName, CancellationToken cancellationToken)
    {
        var fullPath = getFullPath(basePath, path);

        if (!await bucketResourceExists(bucket, fullPath, cancellationToken))
        {
            return null; // Not Found
        }

        var (_relpath, _name, isFolder) = getObjectPathInfo(basePath, path, fullPath);

        if (isFolder)
        {
            // TODO: recursive operation?
            return null;
        }
        else
        {
            var newPath = string.Join('/', [.. fullPath.Split('/')[0..^1], newName]);

            if (await bucketResourceExists(bucket, newPath, cancellationToken))
            {
                return null; // Not Found - TODO: Should allow override?
            }

            await s3.CopyObjectAsync(bucket, fullPath, bucket, newPath, cancellationToken);
            await s3.DeleteObjectAsync(bucket, fullPath, cancellationToken);
        }
 
        return true;
    }

    private async Task<bool> bucketResourceExists(string bucket, string fullPath, CancellationToken cancellationToken) {
        // Use list to support folders
        var response = await s3.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucket,
            Prefix = fullPath,
        }, cancellationToken
        );

        if (response.S3Objects == null || !response.S3Objects.Exists(o => o.Key == fullPath))
        {
            return false;
        }

        return true;
    }

    private static string getFullPath(string basePath, string path)
    {
        return $"{basePath}{path}";
    }

    private static (string path, string name, bool isFolder) getObjectPathInfo(string basePath, string path, string key)
    {
        var isFolder = key.Last() == '/';
        var relPath = basePath == "" ? key : removeFirst(key, basePath);
        var name = isFolder ? key.Split("/")[^2] : key.Split("/")[^1];

        return (relPath, name, isFolder);
    }

    private static string removeFirst(string value, string removeString)
    {
        var index = value.IndexOf(removeString, StringComparison.Ordinal);
        return index < 0 ? value : value.Remove(index, removeString.Length);
    }
}

public class ResourceCompare:IComparer<string> {
    public int Compare(string? nameA, string? nameB) {
        var foldersCompare = (nameB?.Last() == '/').CompareTo(nameA?.Last() == '/');
        if (foldersCompare != 0) return foldersCompare; 
        
        return nameA?.CompareTo(nameB) ?? 0;
    }
}