using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models.Schedules;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(TestSuiteTask), "DeployTestSuite")]
public abstract class ScheduleTask
{
    [JsonPropertyName("type")] public TaskTypeEnum Type { get; protected set; }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class TestSuiteTask : ScheduleTask
{
    [JsonPropertyName("entityId")] public required string EntityId { get; init; }

    [JsonPropertyName("environment")] public required string Environment { get; init; }

    [JsonPropertyName("cpu")] public required int Cpu { get; init; }

    [JsonPropertyName("memory")] public required int Memory { get; init; }

    [JsonPropertyName("profile")] public string? Profile { get; init; }
}

// entity tasks for entity endpoint (entityId is passed as path parameter & not all schedule tasks are entity scheduled tasks)
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(EntityTestSuiteTask), "DeployTestSuite")]
public abstract class EntityScheduleTask : ScheduleTask;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class EntityTestSuiteTask : EntityScheduleTask
{
    [JsonPropertyName("environment")] public required string Environment { get; init; }

    [JsonPropertyName("cpu")] public required int Cpu { get; init; }

    [JsonPropertyName("memory")] public required int Memory { get; init; }

    [JsonPropertyName("profile")] public string? Profile { get; init; }
}