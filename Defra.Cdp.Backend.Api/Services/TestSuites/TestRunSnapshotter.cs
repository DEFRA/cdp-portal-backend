using Defra.Cdp.Backend.Api.Config;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.MonoLambda;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Triggers;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.TestSuites;

public interface ITestRunSnapshotter
{
    Task Snapshot(TestRun testRun, CancellationToken cancellationToken);
}

public class TestRunSnapshotter(MonoLambdaTrigger monoLambdaTrigger, IOptions<TestRunnerOptions> options) : ITestRunSnapshotter
{
    private readonly string? _defaultDashboard = options.Value.SnapshotDashboard;
    
    public async Task Snapshot(TestRun testRun, CancellationToken cancellationToken)
    {
        if (_defaultDashboard == null) return;

        // TODO: we could filter based off test type here, i.e. only snapshot perf tests etc or specific envs
        var triggerEvent = new MonoLambdaTriggerEvent<GrafanaSnapshotTrigger>
        {
            EventType = "create_grafana_snapshots",
            Timestamp = DateTime.UtcNow,
            Payload = new GrafanaSnapshotTrigger
            {
                RequestId = testRun.RunId,
                From = testRun.Created,
                To = testRun.TaskLastUpdate ?? DateTime.UtcNow,
                DashboardNames = [_defaultDashboard]
            }
        };

        await monoLambdaTrigger.Trigger(triggerEvent, testRun.Environment, cancellationToken);
    }
}