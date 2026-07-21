using Defra.Cdp.Backend.Api.Services.Grafana.Models;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Models;

namespace Defra.Cdp.Backend.Api.Tests.Services.MonoLambda.Models;

public class GrafanaPlaygroundTests
{
    private readonly List<PromotionRequestRecord> _requests =
    [
        new()
        {
            RequestedAt = new DateTime(2026, 1, 3),
            Dashboard = new DashboardPromotionRequest { DashboardUid = "737373", ServiceName = "foo", }
        },

        new()
        {
            RequestedAt = new DateTime(2026, 1, 5, 17, 12, 0),
            Dashboard = new DashboardPromotionRequest { DashboardUid = "1234", ServiceName = "foo", }
        },

        new() { RequestedAt = new DateTime(2026, 1, 5), Alert = new AlertPromotionRequest { ServiceName = "foo" } }
    ];

    
    [Fact]
    public void Test_enrich_dashboard_with_request_when_req_is_more_recent()
    {
        var dash = new PlaygroundDashboard
        {
            Uid = "1234",
            Created = new DateTime(2026, 1, 1, 0, 0, 0),
            Updated = new DateTime(2026, 1, 5, 1 , 2, 0),
            Promoted = false,
            Title = "Foo (custom)",
            Url = "https://metrics/foo/1234",
            Version = 2
        };


        var result = dash.AddPromotionRequest(_requests);

        Assert.NotNull(result.PromotionRequest);
        Assert.Equal(result.PromotionRequest.DashboardUid, dash.Uid);
    }
    
    [Fact]
    public void Test_doesnt_enrich_dashboard_with_request_when_req_is_older()
    {
        var dash = new PlaygroundDashboard
        {
            Uid = "1234",
            Created = new DateTime(2026, 1, 1, 0, 0, 0),
            Updated = new DateTime(2026, 1, 10, 1 , 2, 0),
            Promoted = false,
            Title = "Foo (custom)",
            Url = "https://metrics/foo/1234",
            Version = 2
        };


        var result = dash.AddPromotionRequest(_requests);

        Assert.Null(result.PromotionRequest);
    }
    
    [Fact]
    public void Test_doesnt_enrich_dashboard_with_request_when_promoted()
    {
        var dash = new PlaygroundDashboard
        {
            Uid = "1234",
            Created = new DateTime(2026, 1, 1, 0, 0, 0),
            Updated = new DateTime(2026, 1, 4, 1 , 2, 0),
            Promoted = true,
            Title = "Foo (custom)",
            Url = "https://metrics/foo/1234",
            Version = 2
        };


        var result = dash.AddPromotionRequest(_requests);

        Assert.Null(result.PromotionRequest);
    }
    
}