using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.Grafana;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Handlers;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.MonoLambda.Handlers;

public class GrafanaListPlaygroundsHandlerTests
{
    private readonly IGrafanaPlaygroundService _grafanaPlaygroundService = Substitute.For<IGrafanaPlaygroundService>();
    
    [Fact]
    public async Task Test_persists_payload()
    {
        string testMessage = """
                             {
                               "request_id": "1234",
                               "service": "cdp-portal-backend",
                               "dashboards": [],
                               "alerts": []
                             }
                             """;
        
        var handler = new GrafanaListPlaygroundsHandler(_grafanaPlaygroundService, NullLogger<GrafanaSnapshotHandler>.Instance);
        var msg = JsonSerializer.Deserialize<JsonElement>(testMessage);
        await handler.HandleAsync(msg, TestContext.Current.CancellationToken);

        await _grafanaPlaygroundService.Received()
            .UpdatePlaygroundForService(Arg.Is<GrafanaPlaygroundResources>(g => g.RequestId == "1234" && g.Service == "cdp-portal-backend"), Arg.Any<CancellationToken>());
    }
}