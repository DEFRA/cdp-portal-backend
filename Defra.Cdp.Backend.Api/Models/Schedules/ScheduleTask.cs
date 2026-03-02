using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models.Schedules;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TestSuiteTask), "DeployTestSuite")]
public abstract class ScheduleTask
{
    [JsonPropertyName("type")] public TaskTypeEnum Type { get; protected set; }
}

public class TestSuiteTask : ScheduleTask
{
    [JsonPropertyName("entityId")] public string EntityId { get; init; } = default!;

    [JsonPropertyName("environment")] public string Environment { get; init; } = default!;

    [JsonPropertyName("cpu")] public int Cpu { get; init; }

    [JsonPropertyName("memory")] public int Memory { get; init; }

    [JsonPropertyName("profile")] public string? Profile { get; init; }
}

// entity tasks for entity endpoint (entityId is passed as path parameter & not all schedule tasks are entity scheduled tasks)
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(EntityTestSuiteTask), "DeployTestSuite")]
public abstract class EntityScheduleTask : ScheduleTask;

public class EntityTestSuiteTask : EntityScheduleTask
{
    [JsonPropertyName("environment")] public string Environment { get; init; } = default!;

    [JsonPropertyName("cpu")] public int Cpu { get; init; }

    [JsonPropertyName("memory")] public int Memory { get; init; }

    [JsonPropertyName("profile")] public string? Profile { get; init; }
}