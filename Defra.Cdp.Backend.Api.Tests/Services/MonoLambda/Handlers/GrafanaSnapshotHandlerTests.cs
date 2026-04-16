using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Handlers;
using Defra.Cdp.Backend.Api.Services.TestSuites;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.MonoLambda.Handlers;

public class GrafanaSnapshotHandlerTests
{
    private readonly ITestRunService _testRunService = Substitute.For<ITestRunService>();
    

    
    [Fact]
    public async Task Test_snapshot_updates_test_run()
    {
        string testMessage = """
                             {
                               "request_id": "1234",
                               "snapshot_urls": ["http://metrics/snapshot/123"]
                             }
                             """;
        
        _testRunService.FindTestRun(Arg.Is("1234"), Arg.Any<CancellationToken>())
            .Returns(new TestRun { RunId = "1234" });
        
        var handler = new GrafanaSnapshotHandler(_testRunService, NullLogger<GrafanaSnapshotHandler>.Instance);
        var msg = JsonSerializer.Deserialize<JsonElement>(testMessage);
        await handler.HandleAsync(msg, TestContext.Current.CancellationToken);

        await _testRunService.Received().UpdateSnapshots(Arg.Is("1234"), Arg.Any<List<string>>(), Arg.Any<CancellationToken>());
    }
    
    [Fact]
    public async Task Test_snapshot_doesnt_update_if_run_id_doesnt_match()
    {
        string testMessage = """
                             {
                               "request_id": "9999",
                               "snapshot_urls": ["http://metrics/snapshot/123"]
                             }
                             """;
        
        _testRunService.FindTestRun(Arg.Is("1234"), Arg.Any<CancellationToken>())
            .Returns(new TestRun { RunId = "1234" });
        
        var handler = new GrafanaSnapshotHandler(_testRunService, NullLogger<GrafanaSnapshotHandler>.Instance);
        var msg = JsonSerializer.Deserialize<JsonElement>(testMessage);
        await handler.HandleAsync(msg, TestContext.Current.CancellationToken);

        await _testRunService.DidNotReceiveWithAnyArgs().UpdateSnapshots(Arg.Any<string>(), Arg.Any<List<string>>(), Arg.Any<CancellationToken>());
    }
}