using Amazon.S3;
using Amazon.S3.Model;
using Defra.Cdp.Backend.Api.Services.BucketManagement;
using Defra.Cdp.Backend.Api.Services.BucketManagement.Models;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.BucketManagement;

public class BucketManagementServiceTests
{
    private static readonly string s_bucketName = "test-bucket";
    private static readonly DateTime s_modifiedDate = DateTime.Now;
    private const Int64 ONE_HUNDRED_MEGABYTES = 100 * 1024 * 1024;

    private static ListObjectsV2Response filteredListResponse(string prefix = "") {
        return new ListObjectsV2Response
        {
            IsTruncated = false,
            S3Objects = [.. new List<S3Object>([
                new S3Object {
                    Key = "file.txt",
                    LastModified = s_modifiedDate,
                    Size = 1254
                },
                new S3Object {
                    Key = "folder/file-in-folder.txt",
                    LastModified = s_modifiedDate,
                    Size = 751254
                },
                new S3Object {
                    Key = "folder/sub-folder/file-in-folder.txt",
                    LastModified = s_modifiedDate,
                    Size = 3452
                },
                new S3Object {
                    Key = "folder/empty-folder/",
                    LastModified = s_modifiedDate,
                    Size = 0
                }
            ]).Where(o => prefix == "" || o.Key.StartsWith(prefix))]
        };
    }

    [Fact]
    public async Task Test_list_resources_at_root_path()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        s3.ListObjectsV2Async(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(filteredListResponse("")));

        var result = await bucketManagementService.ListBucketResources(s_bucketName, "", "", TestContext.Current.CancellationToken);

        var expected = new List<BucketResource>([
            new BucketResource { Name = "folder", Path = "folder/", Size = 754706, ModifiedDate = s_modifiedDate, IsFolder = true },
            new BucketResource { Name = "file.txt", Path = "file.txt", Size = 1254, ModifiedDate = s_modifiedDate, IsFolder = false },
        ]);
        Assert.Equivalent(expected, result, true);
    }

    [Fact]
    public async Task Test_list_resources_with_base_path()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        s3.ListObjectsV2Async(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(filteredListResponse("folder/")));

        var result = await bucketManagementService.ListBucketResources(s_bucketName, "folder/", "", TestContext.Current.CancellationToken);

        var expected = new List<BucketResource>([
            new BucketResource { Name = "empty-folder", Path = "empty-folder/", Size = 0, ModifiedDate = s_modifiedDate, IsFolder = true },
            new BucketResource { Name = "sub-folder", Path = "sub-folder/", Size = 3452, ModifiedDate = s_modifiedDate, IsFolder = true },
            new BucketResource { Name = "file-in-folder.txt", Path = "file-in-folder.txt", Size = 751254, ModifiedDate = s_modifiedDate, IsFolder = false },
        ]);
        Assert.Equivalent(expected, result, true);
    }

    [Fact]
    public async Task Test_list_resources_with_path()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        s3.ListObjectsV2Async(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(filteredListResponse("folder/")));

        var result = await bucketManagementService.ListBucketResources(s_bucketName, "", "folder/", TestContext.Current.CancellationToken);

        var expected = new List<BucketResource>([
            new BucketResource { Name = "empty-folder", Path = "folder/empty-folder/", Size = 0, ModifiedDate = s_modifiedDate, IsFolder = true },
            new BucketResource { Name = "sub-folder", Path = "folder/sub-folder/", Size = 3452, ModifiedDate = s_modifiedDate, IsFolder = true },
            new BucketResource { Name = "file-in-folder.txt", Path = "folder/file-in-folder.txt", Size = 751254, ModifiedDate = s_modifiedDate, IsFolder = false },
        ]);
        Assert.Equivalent(expected, result, true);
    }

    [Fact]
    public async Task Test_list_resources_with_basePath_and_path()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        s3.ListObjectsV2Async(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(filteredListResponse("folder/sub-folder/")));

        var result = await bucketManagementService.ListBucketResources(s_bucketName, "folder/", "sub-folder/", TestContext.Current.CancellationToken);

        var expected = new List<BucketResource>([
            new BucketResource { Name = "file-in-folder.txt", Path = "sub-folder/file-in-folder.txt", Size = 3452, ModifiedDate = s_modifiedDate, IsFolder = false },
        ]);
        Assert.Equivalent(expected, result, true);
    }

    [Fact]
    public async Task Test_get_resource_with_missing_object()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        s3.ListObjectsV2Async(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(filteredListResponse("folder/sub-folder/missing-file.txt")));

        var result = await bucketManagementService.GetBucketResourceUrl(s_bucketName, "folder/", "sub-folder/missing-file.txt", TestContext.Current.CancellationToken);

        Assert.Equivalent(null, result, true);
    }

    [Fact]
    public async Task Test_get_resource_with_basePath_and_path()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        s3.ListObjectsV2Async(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(filteredListResponse("folder/sub-folder/file-in-folder.txt")));
        s3.GetPreSignedURLAsync(default).ReturnsForAnyArgs(Task.FromResult("https://the-presigned-url.s3.aws.com"));

        var result = await bucketManagementService.GetBucketResourceUrl(s_bucketName, "folder/", "sub-folder/file-in-folder.txt", TestContext.Current.CancellationToken);

        var expected = new BucketResourceUrl { Method = "GET", Url = "https://the-presigned-url.s3.aws.com" };
        Assert.Equivalent(expected, result, true);
    }

    [Fact]
    public async Task Test_get_resource_which_is_a_folder()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        s3.ListObjectsV2Async(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(filteredListResponse("folder/sub-folder/")));

        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await bucketManagementService.GetBucketResourceUrl(s_bucketName, "folder/", "sub-folder/", TestContext.Current.CancellationToken)
        );
        Assert.Equal("Not a file", ex.Message);
    }

    [Fact]
    public async Task Test_start_resource_upload_with_basePath_and_path_using_small_file()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        s3.InitiateMultipartUploadAsync(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(new InitiateMultipartUploadResponse { UploadId = "1234" }));

        var result = await bucketManagementService.StartBucketResourceMultipartUpload(s_bucketName, "folder/", "sub-folder/new-file", 45000, TestContext.Current.CancellationToken);

        var expected = new BucketResourceUpload
        {
            UploadId = "1234",
            Parts = [
                new BucketResourceUploadPart {
                    PartNumber = 1,
                    QueryParams = "uploadId=1234&partNumber=1",
                    ByteStartPosition = 0,
                    ByteEndPosition = 45000
                }
            ]
        };
        Assert.Equivalent(expected, result, true);
        Assert.Equivalent(expected.Parts[0], result.Parts[0], true);
    }

    [Fact]
    public async Task Test_start_resource_upload_with_basePath_and_path_using_large_file()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        s3.InitiateMultipartUploadAsync(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(new InitiateMultipartUploadResponse { UploadId = "1234" }));

        var result = await bucketManagementService.StartBucketResourceMultipartUpload(s_bucketName, "folder/", "sub-folder/new-file", 145000000, TestContext.Current.CancellationToken);

        var expected = new BucketResourceUpload
        {
            UploadId = "1234",
            Parts = [
                new BucketResourceUploadPart {
                    PartNumber = 1,
                    QueryParams = "uploadId=1234&partNumber=1",
                    ByteStartPosition = 0,
                    ByteEndPosition = ONE_HUNDRED_MEGABYTES
                },
                new BucketResourceUploadPart {
                    PartNumber = 2,
                    QueryParams = "uploadId=1234&partNumber=2",
                    ByteStartPosition = ONE_HUNDRED_MEGABYTES,
                    ByteEndPosition = 145000000 - ONE_HUNDRED_MEGABYTES
                }
            ]
        };
        Assert.Equivalent(expected, result, true);
    }

    [Fact]
    public async Task Test_get_resource_upload_part_url_with_basePath_and_path()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        s3.GetPreSignedURLAsync(default).ReturnsForAnyArgs(Task.FromResult("https://the-presigned-url.s3.aws.com"));

        var result = await bucketManagementService.GetBucketResourceMultipartUploadUrl(s_bucketName, "folder/", "sub-folder/new-file", "1234", 1, "md5#", TestContext.Current.CancellationToken);

        var expected = new BucketResourceUrl { Method = "PUT", Url = "https://the-presigned-url.s3.aws.com" };
        Assert.Equivalent(expected, result, true);
    }

    [Fact]
    public async Task Test_complete_resource_upload_with_basePath_and_path()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        s3.CompleteMultipartUploadAsync(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(new CompleteMultipartUploadResponse()));

        await bucketManagementService.CompleteBucketResourceMultipartUpload(s_bucketName, "folder/", "sub-folder/new-file", new CompleteBucketResourceUpload
        {
            UploadId = "1234",
            Parts = [
                new CompleteBucketResourceUploadPart {
                    PartNumber = 1,
                    ETag = "123456"
                }
            ]
        }, TestContext.Current.CancellationToken);

        Assert.True(true);  // Nothing throw an error
    }


    [Fact]
    public async Task Test_create_empty_folder_basePath_and_path()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        s3.ListObjectsV2Async(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(filteredListResponse("folder/sub-folder/new-folder/")));
        s3.PutObjectAsync(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(new PutObjectResponse()));

        var result = await bucketManagementService.CreateEmptyFolder(s_bucketName, "folder/", "sub-folder/new-folder/", TestContext.Current.CancellationToken);
        var expected = new BucketResource { Name = "new-folder", Path = "sub-folder/new-folder/", Size = 0, IsFolder = true };
        Assert.Equivalent(expected.Name, result.Name, true);
        Assert.Equivalent(expected.Path, result.Path, true);
        Assert.Equivalent(expected.Size, result.Size, true);
        Assert.Equivalent(expected.IsFolder, result.IsFolder, true);
    }

    [Fact]
    public async Task Test_create_empty_folder_basePath_and_path_when_not_a_folder()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        s3.PutObjectAsync(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(new PutObjectResponse()));

        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await bucketManagementService.CreateEmptyFolder(s_bucketName, "folder/", "sub-folder/new-folder/file.txt", TestContext.Current.CancellationToken)
        );
        Assert.Equal("Not a folder", ex.Message);
    }

    [Fact]
    public async Task Test_create_empty_folder_basePath_and_path_when_already_exists()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        s3.ListObjectsV2Async(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(filteredListResponse("folder/sub-folder/")));
        s3.PutObjectAsync(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(new PutObjectResponse()));

        var ex = await Assert.ThrowsAsync<ArgumentException>(async () =>
            await bucketManagementService.CreateEmptyFolder(s_bucketName, "folder/", "sub-folder/", TestContext.Current.CancellationToken)
        );
        Assert.Equal("Already exists", ex.Message);
    }
}
