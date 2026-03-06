using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models.Schedules;

// request for schedule endpoint
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ScheduleRequest
{
    [JsonPropertyName("enabled")] public bool Enabled { get; init; } = true;
    [JsonPropertyName("task")] public ScheduleTask Task { get; init; } = default!;
    [JsonPropertyName("config")] public ScheduleConfig Config { get; init; } = default!;
}

// request for entity endpoint
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class EntityScheduleRequest
{
    [JsonPropertyName("enabled")] public bool Enabled { get; init; } = true;
    [JsonPropertyName("task")] public EntityScheduleTask Task { get; init; } = default!;
    [JsonPropertyName("config")] public ScheduleConfig Config { get; init; } = default!;
}
