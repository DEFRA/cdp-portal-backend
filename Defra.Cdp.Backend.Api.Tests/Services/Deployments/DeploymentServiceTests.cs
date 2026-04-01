using Defra.Cdp.Backend.Api.Services.Deployments;

namespace Defra.Cdp.Backend.Api.Tests.Services.Deployments;

public class DeploymentServiceTests
{
    [Fact]
    public void Test_deployment_matcher_handles_blank_string_dates()
    {
        var defaultMatcher = new DeploymentMatchers { };
        var validMatcher =
            new DeploymentMatchers { To = "2026-04-01T09:31:41.201Z", From = "2026-05-01T09:31:41.201Z" };
        var nullFrom  = new DeploymentMatchers { To = "2026-04-01T09:31:41.201Z" };
        var nullTo    = new DeploymentMatchers { From = "2026-04-01T09:31:41.201Z" };
        var blankFrom = new DeploymentMatchers { To = "" };
        var blankTo   = new DeploymentMatchers { From = "" };
        
        
        Assert.Null(Record.Exception(() => { defaultMatcher.Filter(); }));
        Assert.Null(Record.Exception(() => { validMatcher.Filter(); }));
        Assert.Null(Record.Exception(() => { nullFrom.Filter(); }));
        Assert.Null(Record.Exception(() => { nullTo.Filter(); }));
        Assert.Null(Record.Exception(() => { blankFrom.Filter(); }));
        Assert.Null(Record.Exception(() => { blankTo.Filter(); }));
    }
    
    [Fact]
    public void Test_deployment_matcher_rejects_invalid_dates()
    {
        var bothInvalid =
            new DeploymentMatchers { To = "345345345", From = "now" };
        var validTo =
            new DeploymentMatchers { To = "2026-04-01T09:31:41.201Z", From = "now" };
        var validFrom =
            new DeploymentMatchers { To = "helloworld", From = "2026-04-01T09:31:41.201Z" };
        var invalidTo  = new DeploymentMatchers { To = "last week sometime" };
        var invalidFrom    = new DeploymentMatchers { From = "2026" };

        Assert.NotNull(Record.Exception(() => { bothInvalid.Filter(); }));
        Assert.NotNull(Record.Exception(() => { validTo.Filter(); }));
        Assert.NotNull(Record.Exception(() => { validFrom.Filter(); }));
        Assert.NotNull(Record.Exception(() => { invalidTo.Filter(); }));
        Assert.NotNull(Record.Exception(() => { invalidFrom.Filter(); }));
    }
}