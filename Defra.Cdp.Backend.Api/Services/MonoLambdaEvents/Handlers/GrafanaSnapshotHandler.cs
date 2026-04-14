using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.TestSuites;

namespace Defra.Cdp.Backend.Api.Services.MonoLambdaEvents.Handlers;

internal class SnapshotResponse {

    [JsonPropertyName("request_id")] public string? RequestId { get; init; }

    [JsonPropertyName("snapshot_urls")] public List<string> SnapshotUrls { get; init; } = [];
}

public class GrafanaSnapshotHandler(ITestRunService testRunService, ILogger<GrafanaSnapshotHandler> logger) : IMonoLambdaEventHandler
{
    public string EventType => "grafana_snapshots";
    public bool PersistEvents => false;
    
    public async Task HandleAsync(JsonElement message, CancellationToken cancellationToken)
    {
        var response = message.Deserialize<SnapshotResponse>();
        if (response == null)
        {
            throw new Exception("Failed to parse platform state headers");
        }

        if (response.RequestId == null)
        {
            logger.LogWarning("grafana_snapshots has no request_id");
            return;
        } 

        var testRun = await testRunService.FindTestRun(response.RequestId, cancellationToken);
        if (testRun != null)
        {
            logger.LogInformation("Updating grafana snapshots for {RunId}", response.RequestId);
            await testRunService.UpdateSnapshots(response.RequestId, response.SnapshotUrls, cancellationToken);
        }
    }
}