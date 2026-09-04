using Amazon.S3;
using Amazon.S3.Model;
using Azure;
using Defra.Cdp.Backend.Api.Services.BucketManagement;
using Defra.Cdp.Backend.Api.Services.BucketManagement.Models;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.BucketManagement;

public class BucketManagementServiceTests
{
    [Fact]
    public async Task Test_list_resources_at_base_path()
    {
        var s3 = Substitute.For<IAmazonS3>();
        var bucketManagementService = Substitute.For<BucketManagementService>(s3);

        var modifedDate = DateTime.Now;
        var s3Response = new ListObjectsV2Response
        {
            IsTruncated = false,
            S3Objects = [
                new S3Object {
                    Key = "file.txt",
                    LastModified = modifedDate
                },
                new S3Object {
                    Key = "folder/file-in-folder.txt",
                    LastModified = modifedDate
                },
                new S3Object {
                    Key = "folder/sub-folder/file-in-folder.txt",
                    LastModified = modifedDate
                },
                new S3Object {
                    Key = "folder/empty-folder/",
                    LastModified = modifedDate
                }
            ]
        };

        s3.ListObjectsV2Async(default, TestContext.Current.CancellationToken).ReturnsForAnyArgs(Task.FromResult(s3Response));

        var result = await bucketManagementService.ListBucketResources("test-bucket", "", "", TestContext.Current.CancellationToken);

        var expected = new List<BucketResource>([
            new BucketResource { Name = "folder", Path = "folder/", Size = 0, ModifiedDate = modifedDate, IsFolder = true },
            new BucketResource { Name = "file.txt", Path = "file.txt", Size = 0, ModifiedDate = modifedDate, IsFolder = false },
        ]);
        Assert.Equivalent(expected, result, false);
    }
}