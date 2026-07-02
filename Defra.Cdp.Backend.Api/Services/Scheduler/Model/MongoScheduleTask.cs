using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Models.Schedules;
using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Scheduler;
using Defra.Cdp.Backend.Api.Services.Scheduler.TestSuiteDeployment;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Defra.Cdp.Backend.Api.Services.Scheduler.Model;

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MongoTestSuiteScheduleTask), nameof(TaskTypeEnum.DeployTestSuite))]
[JsonDerivedType(typeof(MongoDeployServiceScheduleTask), nameof(TaskTypeEnum.DeployService))]
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

        if (ScheduleExecutionWindow.IsDue(nextRunAt))
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

public class MongoDeployServiceScheduleTask : MongoScheduleTask
{
    [JsonIgnore] public override TaskTypeEnum Type { get; protected set; } = TaskTypeEnum.DeployService;
    public override required string EntityId { get; init; }
    public required List<string> Environments { get; init; }

    public override async Task ExecuteAsync(
        IServiceProvider services,
        DateTime? nextRunAt,
        ILogger<object> logger,
        CancellationToken ct)
    {
        var artifactsService = services.GetRequiredService<IDeployableArtifactsService>();
        var serviceDeploymentExecutor = services.GetRequiredService<IServiceDeploymentExecutor>();

        if (!ScheduleExecutionWindow.IsDue(nextRunAt))
        {
            logger.LogWarning(
                "Not executing service deployment for {service} with next run at {nextRunAt}",
                EntityId,
                nextRunAt);
            return;
        }

        var latestArtifact = await artifactsService.FindLatest(EntityId, ct);
        if (latestArtifact == null)
        {
            logger.LogError(
                "Could not find latest artifact for service {service}. Skipping scheduled deployment.",
                EntityId);
            return;
        }

        var user = new UserDetails
        {
            Id = ScheduledTestRunConstants.UserId,
            DisplayName = ScheduledTestRunConstants.DisplayName
        };

        foreach (var environment in Environments)
        {
            await serviceDeploymentExecutor.DeployAsync(
                EntityId,
                latestArtifact.Tag,
                environment,
                user,
                ct);
        }
    }
}