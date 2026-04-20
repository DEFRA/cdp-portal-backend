using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.MonoLambda.Triggers;


public record GrafanaSnapshotTrigger : MongoLambdaTriggerPayload
{
    [JsonPropertyName("dashboard_names")] 
    public List<string> DashboardNames { get; init; } = [];

    [JsonPropertyName("dashboard_uids")]
    public List<string> DashboardUids { get; init; } = [];
    
    [JsonPropertyName("from")]
    public required DateTime From { get; init; }
    
    [JsonPropertyName("to")]
    public required DateTime To { get; init; }

    [JsonPropertyName("snapshot_expiry_seconds")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public long? ExpirySeconds { get; init; }

    [JsonPropertyName("request_id")]
    public required string RequestId { get; init; }
}