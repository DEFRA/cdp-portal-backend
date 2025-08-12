using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Model;

public class TenantDatabasePayload
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }
    [JsonPropertyName("rds")] public required List<RdsDatabase> RdsDatabases { get; init; }
}

public class RdsDatabase
{
    [JsonPropertyName("service")]  public string? Service { get; init; }
    [JsonPropertyName("databaseName")]  public required string DatabaseName { get; init; }
    [JsonPropertyName("endpoint")]  public required string Endpoint { get; init; }
    [JsonPropertyName("readerEndpoint")]  public required string ReaderEndpoint { get; init; }
    [JsonPropertyName("port")]  public required int Port { get; init; } 
    [JsonPropertyName("engine")]  public string? Engine { get; init; }
    [JsonPropertyName("engineVersion")]  public string? EngineVersion { get; init; }
    [JsonPropertyName("earliestRestorableTime")]  public DateTime? EarliestRestorableTime { get; init; }
    [JsonPropertyName("latestRestorableTime")]  public DateTime? LatestRestorableTime { get; init; }
    [JsonPropertyName("backupRetentionPeriod")]  public required int BackupRetentionPeriod { get; init; }
}