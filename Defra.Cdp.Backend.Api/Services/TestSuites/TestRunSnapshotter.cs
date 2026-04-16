using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.MonoLambda;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Triggers;

namespace Defra.Cdp.Backend.Api.Services.TestSuites;

public interface ITestRunSnapshotter
{
    Task Snapshot(TestRun testRun, CancellationToken cancellationToken);
}

public class TestRunSnapshotter(MonoLambdaTrigger monoLambdaTrigger) : ITestRunSnapshotter
{
    private readonly List<string> _defaultDashboard = [""];
    
    public async Task Snapshot(TestRun testRun, CancellationToken cancellationToken)
    {
        // TODO: we could filter based off test type here, i.e. only snapshot perf tests etc or specific envs
        var triggerEvent = new MonoLambdaTriggerEvent<GrafanaSnapshotTrigger>
        {
            EventType = "grafana_snapshots",
            Timestamp = DateTime.UtcNow,
            Payload = new GrafanaSnapshotTrigger
            {
                RequestId = testRun.RunId,
                From = testRun.Created,
                To = testRun.TaskLastUpdate ?? DateTime.UtcNow,
                DashboardNames = _defaultDashboard
            }
        };

        await monoLambdaTrigger.Trigger(triggerEvent, testRun.Environment, cancellationToken);
    }
}