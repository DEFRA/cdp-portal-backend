using Amazon.S3;
using Amazon.S3.Model;
using Azure;
using Defra.Cdp.Backend.Api.Services.BucketManagement;
using Defra.Cdp.Backend.Api.Services.BucketManagement.Models;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.BucketManagement;

public class BucketManagementServiceTests
{
    private static readonly string s_bucketName = "test-bucket";
    private static readonly DateTime s_modifiedDate = DateTime.Now;

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
        Assert.Equivalent(expected, result, false);
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
        Assert.Equivalent(expected, result, false);
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
        Assert.Equivalent(expected, result, false);
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
        Assert.Equivalent(expected, result, false);
    }
}
