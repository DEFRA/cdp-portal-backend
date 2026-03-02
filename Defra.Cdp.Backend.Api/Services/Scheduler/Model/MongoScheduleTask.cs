using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models.Schedules;
using Defra.Cdp.Backend.Api.Services.scheduler.TestSuiteDeployment;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Services.scheduler.Model;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MongoTestSuiteScheduleTask), nameof(TaskTypeEnum.DeployTestSuite))]
public abstract class MongoScheduleTask
{
    [BsonRepresentation(BsonType.String)]
    [JsonIgnore]
    public abstract TaskTypeEnum Type { get; protected set; }

    public abstract string? EntityId { get; init; }

    public abstract Task ExecuteAsync(
        IServiceProvider services,
        DateTime? nextRunAt,
        ILogger<object> logger,
        CancellationToken ct);
}

public class MongoTestSuiteScheduleTask : MongoScheduleTask
{
    [JsonIgnore] public override TaskTypeEnum Type { get; protected set; } = TaskTypeEnum.DeployTestSuite;
    public override required string EntityId { get; init; }
    public string Environment { get; init; } = default!;
    public int Cpu { get; init; } = default!;
    public int Memory { get; init; } = default!;
    public string? Profile { get; init; }

    public override async Task ExecuteAsync(
        IServiceProvider services,
        DateTime? nextRunAt,
        ILogger<object> logger,
        CancellationToken ct)
    {
        var deployer = services.GetRequiredService<ITestSuiteDeployer>();

        var now = DateTime.UtcNow;
        var tolerance = TimeSpan.FromMinutes(5);
        var shouldExecute =
            nextRunAt.HasValue &&
            nextRunAt.Value >= now - tolerance;

        if (shouldExecute)
        {
            await deployer.DeployAsync(
                EntityId,
                Environment,
                Cpu,
                Memory,
                Profile,
                ct);
        }
        else
        {
            logger.LogWarning(
                "Not executing test-suite {testSuite} to {environment} with next run at {nextRunAt}",
                EntityId, Environment, nextRunAt);
        }
    }
}