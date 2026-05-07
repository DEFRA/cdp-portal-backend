using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.Create.Models;

namespace Defra.Cdp.Backend.Api.Tests.Services.Create.Models;

public class CreateResourceRequestTests
{

    [Fact]
    public void Test_polymophic_deserialization()
    {
        var json = """
                   {
                       "resourceType": "s3", 
                       "service": "foo",
                       "bucketName": "my-bucket",
                       "environment": "prod",
                       "useBranch": "my-branch",
                       "runId": "new bucket",
                       "prTitle": "new bucket for foo"
                   }
                   """;

        var result = JsonSerializer.Deserialize<CreateResourceRequest>(json);
        var req = Assert.IsType<CreateS3BucketRequest>(result);
        Assert.Equal("foo", req.Service);
        Assert.Equal("my-bucket", req.BucketName);
        Assert.Equal("prod", req.Environment);
        Assert.Equal("my-branch", req.UseBranch);
        Assert.Equal("new bucket", req.RunId);
        Assert.Equal("new bucket for foo", req.PrTitle);
    }

}