using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.Create;
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


    [Fact]
    public void Test_deserialize_github_response()
    {
        var json = """
                   {
                   "workflow_run_id": 25552427454,
                   "run_url": "https://api.github.com/repos/DEFRA/cdp-tenant-config/actions/runs/25552427454",
                   "html_url": "https://github.com/DEFRA/cdp-tenant-config/actions/runs/25552427454"
                   }
                   """;
        
        var result = JsonSerializer.Deserialize<GitHubTriggerWorkflowResponse>(json);
        Assert.NotNull(result);
    }
}