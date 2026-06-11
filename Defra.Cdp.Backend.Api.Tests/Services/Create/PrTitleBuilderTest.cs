using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;

namespace Defra.Cdp.Backend.Api.Tests.Services.Create;

public class PrTitleBuilderTest
{
    [Fact]
    public void Test_title_builder_works()
    {
        var prTitle = PrTitleBuilder.Build(new CreateTenantResourceRequest
        {
            S3Buckets =
            [
                new CreateTenantS3Bucket { Service = "foo-backend", Environments = "tenant", Name = "foo-bucket" }
            ],
            SnsTopics = [ 
                new CreateTenantSnsTopic { Environments = "dev", Service = "bar-ui", Name = "bar"},
                new CreateTenantSnsTopic { Environments = "dev", Service = "foo-backend", Name = "baz"}
            ]
        });

        Assert.NotNull(prTitle);
        Assert.Equal("Tenant: Create 1 S3, 2 SNS, for bar-ui,foo-backend", prTitle);
    }
}