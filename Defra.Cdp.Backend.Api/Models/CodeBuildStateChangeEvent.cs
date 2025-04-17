using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Migrations;

namespace Defra.Cdp.Backend.Api.Models;

public sealed record CodeBuildLambdaEvent(
    [property: JsonPropertyName("cdp-migration-id")]
    string CdpMigrationId,
    [property: JsonPropertyName("build-id")]
    string BuildId,
    [property: JsonPropertyName("account")]
    string Account,
    [property: JsonPropertyName("time")] 
    DateTime Time,
    [property: JsonPropertyName("request")] 
    DatabaseMigrationRequest? Request = null
);

public sealed record CodeBuildStateChangeEvent(

    [property: JsonPropertyName("detail-type")]
    string DetailType,
    [property: JsonPropertyName("account")]
    string Account,
    [property: JsonPropertyName("detail")]
    Detail Detail,
    [property: JsonPropertyName("time")]
    DateTime Time
);

public sealed record Detail(
    [property: JsonPropertyName("build-status")]
    string BuildStatus,

    [property: JsonPropertyName("project-name")]
    string ProjectName,
    
    [property: JsonPropertyName("build-id")]
    string BuildId,
    
    [property: JsonPropertyName("phases")] List<Phase> Phases,
    
    [property: JsonPropertyName("current-phase")] string CurrentPhase
);

public sealed record Phase(
    [property: JsonPropertyName("start-time")]
    DateTime StartTime,
    [property: JsonPropertyName("end-time")]
    DateTime? EndTime,
    [property: JsonPropertyName("duration-in-seconds")]
    int? DurationInSeconds,
    [property: JsonPropertyName("phase-type")]
    string PhaseType,
    [property: JsonPropertyName("phase-status")]
    string? PhaseStatus
    
);
