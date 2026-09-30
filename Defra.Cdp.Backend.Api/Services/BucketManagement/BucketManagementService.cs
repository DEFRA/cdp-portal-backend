using System.Globalization;
using Amazon.S3;
using Amazon.S3.Model;
using AwsSignatureVersion4.Private;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.BucketManagement.Models;

namespace Defra.Cdp.Backend.Api.Services.BucketManagement;

/**
 *  Manage S3 Buckets where objects are treated like Files and Folders in a filesystem
 */
public interface IBucketManagementService
{
    Task<List<BucketResource>?> ListBucketResources(string bucket, string basePath, string path, CancellationToken cancellationToken);
    Task<BucketResourceTreeNode?> GetBucketResourcesTree(string bucket, string basePath, string path, CancellationToken cancellationToken);
    Task<BucketResourceUrl?> GetBucketResourceUrl(string bucket, string basePath, string path, CancellationToken cancellationToken);

    Task<BucketResourceUpload> StartBucketResourceMultipartUpload(string bucket, string basePath, string path, Int128 size, UserDetails user, CancellationToken cancellationToken);
    Task<BucketResourceUrl> GetBucketResourceMultipartUploadUrl(string bucket, string basePath, string path, string uploadId, int partNumber, string contentMd5, CancellationToken cancellationToken);
    Task CompleteBucketResourceMultipartUpload(string bucket, string basePath, string path, CompleteBucketResourceUpload completeBucketResourceUpload, CancellationToken cancellationToken);
    Task<BucketResource> CreateEmptyFolder(string bucket, string basePath, string path, UserDetails user, CancellationToken cancellationToken);
    Task DeleteBucketResource(string bucket, string basePath, string path, UserDetails user, CancellationToken cancellationToken);
}

public class BucketManagementService(IAmazonS3 s3, ILogger<BucketManagementService> logger):IBucketManagementService
{
    private const int PRE_SIGNED_URL_TTL_SECONDS = 10;  // Keep as small as possible
    private const Int64 UPLOAD_PART_SIZE_BYTES = 100 * 1024 * 1024; // 100MB
    private const string ISO_FORMAT = "yyyy-MM-dd\\THH:mm:ss.fffK";

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
            logger.LogInformation("Listing bucket{bucket} for prefix {fullPath}", bucket, fullPath);
            response = await s3.ListObjectsV2Async(request, cancellationToken);

            if (response.S3Objects == null)
            {
                logger.LogWarning("No listings for bucket {bucket} for prefix {fullPath}", bucket, fullPath);
                return null; // Not Found
            }

            logger.LogInformation("Found {count} resources listing bucket {bucket} for prefix {fullPath}", response.S3Objects.Count, bucket, fullPath);

            foreach (var s3Object in response.S3Objects)
            {
                var (relPath, name, _) = getObjectPathInfo(basePath, s3Object.Key);
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
                            resource.folderModifedDateResourceKey = s3Object.Key;
                        }
                    }
                    else
                    {
                        resources.Add($"{groupedFolderName}/", new BucketResource
                        {
                            Name = groupedFolderName,
                            ModifiedDate = s3Object.LastModified,
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
                        ModifiedDate = s3Object.LastModified,
                        Size = s3Object.Size ?? 0,
                        Path = relPath,
                        IsFolder = false
                    });
                }

            }

            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated ?? false);

        return await enrichWithMetadata([.. resources.Values], bucket, basePath, cancellationToken);
    }

    public async Task<BucketResourceTreeNode?> GetBucketResourcesTree(string bucket, string basePath, string path, CancellationToken cancellationToken) {
        var fullPath = getFullPath(basePath, path);

        var request = new ListObjectsV2Request
        {
            BucketName = bucket,
            Prefix = basePath
        };

        var tree = new BucketResourceTreeNode
        {
            Path = "",
            IsCurrent = path == ""
        };
        ListObjectsV2Response response;

        do
        {
            logger.LogInformation("Listing bucket{bucket} for prefix {fullPath}", bucket, basePath);
            response = await s3.ListObjectsV2Async(request, cancellationToken);

            if (response.S3Objects == null)
            {
                logger.LogWarning("No listings for bucket {bucket} for prefix {fullPath}", bucket, basePath);
                return null; // Not Found
            }

            logger.LogInformation("Found {count} resources listing bucket {bucket} for prefix {fullPath}", response.S3Objects.Count, bucket, basePath);

            foreach (var s3Object in response.S3Objects)
            {
                var currentNode = tree.SubNodes;
                var subKey = removeFirst(s3Object.Key, basePath);
                var folderParts = subKey.Split('/')[0..^1]; // Only folders
                var index = 0;
                foreach (var part in folderParts)
                {
                    index++;
                    var currentSubPath = string.Join("/", folderParts[0..index]) + "/";
                    var currentPath = $"{basePath}{currentSubPath}";

                    if (!currentNode.ContainsKey(part))
                    {
                        currentNode[part] = new BucketResourceTreeNode
                        {
                            Path = currentSubPath,
                            IsCurrent = path == currentSubPath
                        };
                    }
                    
                    if (fullPath.Contains(currentPath))
                    {
                        currentNode = currentNode[part].SubNodes;
                    }
                    else
                    {
                        break;  // No point walking further down
                    }
                }
            }

            request.ContinuationToken = response.NextContinuationToken;
        }
        while (response.IsTruncated ?? false);

        return tree;
    }

    public async Task<BucketResourceUrl?> GetBucketResourceUrl(string bucket, string basePath, string path, CancellationToken cancellationToken)
    {
        var fullPath = getFullPath(basePath, path);

        if (fullPath.Last() == '/')
        {
            throw new ArgumentException("Not a file");
        }

        if (!await bucketResourceExists(bucket, fullPath, cancellationToken))
        {
            return null; // Not Found
        }

        var request = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = fullPath,
            Expires = DateTime.UtcNow.AddSeconds(PRE_SIGNED_URL_TTL_SECONDS),
            Verb = HttpVerb.GET,
            ResponseHeaderOverrides = {
                ContentDisposition = "attachment"
            }   
        };

        logger.LogInformation("Getting preSigned GET URL for bucket {bucket} key {fullPath}", bucket, fullPath);
        var url = await s3.GetPreSignedURLAsync(request);
        logger.LogInformation("Successfully got preSigned GET URL for bucket {bucket} key {fullPath}", bucket, fullPath);
        
        return new BucketResourceUrl
        {
            Method = "GET",
            Url = normaliseLocalUrl(url)
        };
    }

    public async Task<BucketResourceUpload> StartBucketResourceMultipartUpload(string bucket, string basePath, string path, Int128 size, UserDetails user, CancellationToken cancellationToken)
    {
        var fullPath = getFullPath(basePath, path);

        // TODO: Calc part size based on size?
        var numParts = ((size - 1) / UPLOAD_PART_SIZE_BYTES) + 1; // Int division, rounding up
        var parts = new List<BucketResourceUploadPart>();

        var request = new InitiateMultipartUploadRequest
        {
            BucketName = bucket,
            Key = fullPath
        };

        var (_, createdDate) = await getBucketResourceMetadata(bucket, fullPath, cancellationToken);
        addMetadata(request.Metadata, user, createdDate ?? DateTime.UtcNow);

        var response = await s3.InitiateMultipartUploadAsync(request, cancellationToken);
    
        var uploadId = response.UploadId;

        Int128 currentPosition = 0;
        for (var partNumber = 0; partNumber < numParts; partNumber++)
        {
            var endPosition = Int128.Min(
              currentPosition + UPLOAD_PART_SIZE_BYTES,
              size
            );

            var part = new BucketResourceUploadPart
            {
                PartNumber = partNumber + 1,
                ByteStartPosition = currentPosition,
                ByteEndPosition = endPosition,
                QueryParams = $"uploadId={uploadId}&partNumber={partNumber + 1}"
            };

            parts.Add(part);
            currentPosition += UPLOAD_PART_SIZE_BYTES;
        }

        return new BucketResourceUpload
        {
            UploadId = uploadId,
            Parts = [.. parts]
        };
    }

    public async Task<BucketResourceUrl> GetBucketResourceMultipartUploadUrl(string bucket, string basePath, string path, string uploadId, int partNumber, string contentMd5, CancellationToken cancellationToken) {
        var fullPath = getFullPath(basePath, path);

        var url = await s3.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = fullPath,
            Expires = DateTime.UtcNow.AddSeconds(PRE_SIGNED_URL_TTL_SECONDS),
            Verb = HttpVerb.PUT,
            UploadId = uploadId,
            PartNumber = partNumber,
            Headers = {
                ContentMD5 = contentMd5
            }
        });

        return new BucketResourceUrl
        {
            Method = "PUT",
            Url = normaliseLocalUrl(url)
        };
    }

    public async Task CompleteBucketResourceMultipartUpload(string bucket, string basePath, string path, CompleteBucketResourceUpload completeBucketResourceUpload, CancellationToken cancellationToken) {
        var fullPath = getFullPath(basePath, path);

        await s3.CompleteMultipartUploadAsync(new CompleteMultipartUploadRequest {
            BucketName = bucket,
            Key = fullPath,
            UploadId = completeBucketResourceUpload.UploadId,
            PartETags = [.. completeBucketResourceUpload.Parts.Select(part => new PartETag{
                PartNumber = part.PartNumber,
                ETag = part.ETag
            })]
        }, cancellationToken);
    }

    public async Task<BucketResource> CreateEmptyFolder(string bucket, string basePath, string path, UserDetails user, CancellationToken cancellationToken)
    {
        var fullPath = getFullPath(basePath, path);

        if (fullPath.Last() != '/')
        {
            throw new ArgumentException("Not a folder");
        }

        if (await bucketResourceExists(bucket, fullPath, cancellationToken))
        {
            throw new ArgumentException("Already exists");
        }

        var createdDate = DateTime.UtcNow;

        var request = new PutObjectRequest
        {
            BucketName = bucket,
            Key = fullPath
        };
        addMetadata(request.Metadata, user, createdDate);

        var response = await s3.PutObjectAsync(request, cancellationToken);

        var (relPath, name, isFolder) = getObjectPathInfo(basePath, fullPath);

        return new BucketResource
        {
            Name = name,
            CreatedDate = createdDate,
            ModifiedDate = createdDate,
            Size = response.Size ?? 0,
            Path = relPath,
            IsFolder = isFolder,
            User = user
        };
    }

    public async Task DeleteBucketResource(string bucket, string basePath, string path, UserDetails user, CancellationToken cancellationToken)
    {
        var fullPath = getFullPath(basePath, path);
        var isFolder = fullPath.Last() == '/';

        if (isFolder)
        {
            if (!await isFolderEmpty(bucket, fullPath, cancellationToken))
            {
                throw new ArgumentException("Folder is not empty");
            }
        }

        var (_, createdDate) = await getBucketResourceMetadata(bucket, fullPath, cancellationToken);
        await setBucketResourceMetadata(bucket, fullPath, user, createdDate, cancellationToken);

        var request = new DeleteObjectRequest
        {
            BucketName = bucket,
            Key = fullPath
        };

        logger.LogInformation("Deleting object for bucket {bucket} key {fullPath}", bucket, fullPath);
        await s3.DeleteObjectAsync(request, cancellationToken);
        logger.LogInformation("Sucessfully deleted object for bucket {bucket} key {fullPath}", bucket, fullPath);
    }

    private async Task<bool> bucketResourceExists(string bucket, string fullPath, CancellationToken cancellationToken)
    {
        // Use list to support folders
        var response = await s3.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucket,
            Prefix = fullPath,
        }, cancellationToken);

        if (response.S3Objects == null || response.S3Objects.Count == 0) return false;

        var isFolder = fullPath.Last() == '/';
        if (isFolder) return true;

        return response.S3Objects.Exists(o => o.Key == fullPath);
    }

    private async Task<bool> isFolderEmpty(string bucket, string fullPath, CancellationToken cancellationToken)
    {
        var response = await s3.ListObjectsV2Async(new ListObjectsV2Request
        {
            BucketName = bucket,
            Prefix = fullPath,
            MaxKeys = 10
        }, cancellationToken);

        if (response.S3Objects == null || response.S3Objects.Count == 0) return false;

        return response.S3Objects.Count == 1 && response.S3Objects.First().Key == fullPath;
    }

    private static string getFullPath(string basePath, string path)
    {
        return $"{basePath}{path}";
    }

    private static (string path, string name, bool isFolder) getObjectPathInfo(string basePath, string key)
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

    // Workaround an issue with Floci forcing https URLs on local even when not configured 
    private static string normaliseLocalUrl(string url)
    {
        if (url.StartsWith("https://localhost"))
        {
            return $"http://localhost{removeFirst(url, "https://localhost")}";
        }

        return url;
    }

    private static void addMetadata(MetadataCollection metadata, UserDetails user, DateTime? createdDate)
    {
        metadata.Add("userId", user.Id);
        metadata.Add("userDisplayName", user.DisplayName);
        if (createdDate != null)
        {
            metadata.Add("createdDate", createdDate?.ToString(ISO_FORMAT));
        }
    }

    private async Task<(UserDetails? user, DateTime? createdDate)> getBucketResourceMetadata(string bucket, string fullPath, CancellationToken cancellationToken)
    {
        try
        {
            logger.LogInformation("Getting metadata for bucket {bucket} key {fullPath}", bucket, fullPath);
            var response = await s3.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = bucket,
                Key = fullPath,
            }, cancellationToken);
            logger.LogInformation("Successfully got metadata for bucket {bucket} key {fullPath}", bucket, fullPath);

            var metadata = response.Metadata;

            DateTime? cratedDate = null;
            try
            {
                cratedDate = DateTime.ParseExact(metadata["createdDate"], ISO_FORMAT, CultureInfo.InvariantCulture).ToUniversalTime();
            }
            catch { }

            return (
                metadata["userId"] == null ? null : new UserDetails
                {
                    Id = metadata["userId"],
                    DisplayName = metadata["userDisplayName"] ?? ""
                },
                cratedDate
            );
        }
        catch
        {
            return (
                null,
                null
            );
        }
    }

    private async Task setBucketResourceMetadata(string bucket, string fullPath, UserDetails user, DateTime? createdDate, CancellationToken cancellationToken)
    {
        var metadataRequest = new CopyObjectRequest
        {
            SourceBucket = bucket,
            SourceKey = fullPath,
            DestinationBucket = bucket,
            DestinationKey = fullPath
        };
        addMetadata(metadataRequest.Metadata, user, createdDate);

        logger.LogInformation("Setting metadata for bucket {bucket} key {fullPath}", bucket, fullPath);
        await s3.CopyObjectAsync(metadataRequest, cancellationToken);
        logger.LogInformation("Sucessfully set metadata for bucket {bucket} key {fullPath}", bucket, fullPath);
    }

    private async Task<List<BucketResource>> enrichWithMetadata(List<BucketResource> resources, string bucket, string basePath, CancellationToken cancellationToken) {
        await Task.WhenAll(resources.Select(async resource =>
        {           
            var (user, createdDate) = await getBucketResourceMetadata(bucket, basePath + resource.Path, cancellationToken);
            resource.User = user;
            resource.CreatedDate = createdDate;

            // Override user with last modified child's user
            if (resource.folderModifedDateResourceKey != null)
            {
                var (childUser, _) = await getBucketResourceMetadata(bucket, resource.folderModifedDateResourceKey, cancellationToken);
                resource.User = childUser;
            }
        }));

        return resources;
    }
}

public class ResourceCompare:IComparer<string> {
    public int Compare(string? nameA, string? nameB) {
        var foldersCompare = (nameB?.Last() == '/').CompareTo(nameA?.Last() == '/');
        if (foldersCompare != 0) return foldersCompare; 
        
        return nameA?.CompareTo(nameB) ?? 0;
    }
}