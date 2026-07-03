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
                               "event_type": "grafana_list_playgrounds",
                               "request_id": "dd808b65-7cb9-42db-b606-9f73a93de9ad",
                               "service": "cdp-uploader",
                               "dashboards": [
                                 {
                                   "uid": "d0d9cc1f-abef-44ca-be1a-ee503b737326",
                                   "title": "cdp-uploader (custom)",
                                   "version": 2,
                                   "url": "/d/d0d9cc1f-abef-44ca-be1a-ee503b737326/cdp-uploader-custom",
                                   "created": "2026-06-18T15:21:13Z",
                                   "updated": "2026-06-18T15:27:02Z"
                                 },
                                 {
                                   "uid": "a5bb51c6-ead8-4263-bb6f-b2edb18f1b4c",
                                   "title": "cdp-uploader (custom)",
                                   "version": 1,
                                   "url": "/d/a5bb51c6-ead8-4263-bb6f-b2edb18f1b4c/cdp-uploader-custom",
                                   "created": "2026-06-05T07:45:10Z",
                                   "updated": "2026-06-05T07:45:10Z"
                                 }
                               ],
                               "alerts": []
                             }
                             """;
        
        var handler = new GrafanaListPlaygroundsHandler(_grafanaPlaygroundService, NullLogger<GrafanaSnapshotHandler>.Instance);
        var msg = JsonSerializer.Deserialize<JsonElement>(testMessage);
        await handler.HandleAsync(msg, TestContext.Current.CancellationToken);

        await _grafanaPlaygroundService.Received()
            .UpdatePlaygroundForService(Arg.Is<GrafanaPlaygroundResources>(g => g.RequestId == "dd808b65-7cb9-42db-b606-9f73a93de9ad" && g.Service == "cdp-uploader"), Arg.Any<CancellationToken>());
    }
}