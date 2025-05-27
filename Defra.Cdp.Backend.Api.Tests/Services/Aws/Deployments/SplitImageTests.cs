using Defra.Cdp.Backend.Api.Services.Aws.Deployments;

namespace Defra.Cdp.Backend.Api.Tests.Services.Aws.Deployments;

public class SplitImageTests
{
    [Fact]
    public void TestSplitImage()
    {
        var resp = TaskStateChangeEventHandler.SplitImage(
            "000000000.dkr.ecr.eu-west-2.amazonaws.com/cdp-portal-deployables-backend:0.1.0");

        Assert.Equal("cdp-portal-deployables-backend", resp.Item1);
        Assert.Equal("0.1.0", resp.Item2);
    }

    [Fact]
    public void TestSplitImageLatest()
    {
        var resp = TaskStateChangeEventHandler.SplitImage(
            "000000000.dkr.ecr.eu-west-2.amazonaws.com/cdp-portal-deployables-backend:latest");

        Assert.Equal("cdp-portal-deployables-backend", resp.Item1);
        Assert.Equal("latest", resp.Item2);
    }

    [Fact]
    public void TestSplitSha()
    {
        var resp = TaskStateChangeEventHandler.SplitSha(
            "000000000.dkr.ecr.eu-west-2.amazonaws.com/cdp-portal-deployables-backend@sha256:c0ac9c077606aec2d438fd47c5fb9b305859645cf23f24c1ab0cd4739f8cffef");

        Assert.Equal("cdp-portal-deployables-backend", resp.Item1);
        Assert.Equal("sha256:c0ac9c077606aec2d438fd47c5fb9b305859645cf23f24c1ab0cd4739f8cffef", resp.Item2);
    }

}