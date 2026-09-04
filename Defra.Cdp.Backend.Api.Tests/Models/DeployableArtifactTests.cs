using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Utils;

namespace Defra.Cdp.Backend.Api.Tests.Models;

public class DeployableArtifactTests
{
    [Fact]
    public void TestSqlEcrEventToDeploymentArtifact()
    {
        var res  = DeployableArtifact.FromEcrEvent(
            new SqsEcrEvent(new SqsEcrEventDetail("SUCCESS", "foo", "0.0.1", "PUSH", "sha256:1234"), "ECR Image Action"));
        
        Assert.Equal("foo", res.Repo);
        Assert.Equal("0.0.1", res.Tag);
        Assert.Equal(SemVer.SemVerAsLong("0.0.1"), res.SemVer);
        Assert.Equal("sha256:1234", res.Sha256);
    }

    [Fact]
    public void TestSqlEcrEventToDeploymentArtifactWithPrereleaseTag()
    {
        var res = DeployableArtifact.FromEcrEvent(
            new SqsEcrEvent(new SqsEcrEventDetail("SUCCESS", "foo", "0.0.1-rc.1", "PUSH", "sha256:1234"),
                "ECR Image Action"));

        Assert.Equal("foo", res.Repo);
        Assert.Equal("0.0.1-rc.1", res.Tag);
        Assert.Equal(SemVer.SemVerAsLong("0.0.1-rc.1"), res.SemVer);
        Assert.Equal("sha256:1234", res.Sha256);
    }
}