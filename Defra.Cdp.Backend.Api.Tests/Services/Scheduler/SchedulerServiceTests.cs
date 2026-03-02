using Defra.Cdp.Backend.Api.Services.scheduler;
using Defra.Cdp.Backend.Api.Services.scheduler.Model;
using Defra.Cdp.Backend.Api.Services.Scheduler.Model;
using Defra.Cdp.Backend.Api.Services.scheduler.TestSuiteDeployment;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace Defra.Cdp.Backend.Api.Tests.Services.scheduler;

public class SchedulerDomainTests
{
    [Fact]
    public void RecalculateNextRun_SetsNextRunAt_WhenNextExists()
    {
        var task = new MongoTestSuiteScheduleTask { EntityId = "suite", Environment = "dev", Cpu = 1, Memory = 256 };

        var config = new MongoCronRecurringConfig { Expression = "*/1 * * * *" }; // every minute
        var schedule = new MongoSchedule(true, config.Expression, "desc", task, config,
            new MongoUserDetails { Id = "u", DisplayName = "n" });

        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var next = schedule.RecalculateNextRun(baseTime);


        Assert.NotNull(next);
        Assert.True(next.Value > baseTime);
        Assert.Equal(next, schedule.NextRunAt);
    }

    [Fact]
    public void RecalculateNextRun_ReturnsNull_WhenNextAfterEndDate()
    {
        var task = new MongoTestSuiteScheduleTask { EntityId = "suite", Environment = "dev", Cpu = 1, Memory = 256 };

        var config = new MongoCronRecurringConfig
        {
            Expression = "*/1 * * * *", EndDate = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddMinutes(-1)
        };
        var schedule = new MongoSchedule(true, config.Expression, "desc", task, config,
            new MongoUserDetails { Id = "u", DisplayName = "n" });

        var baseTime = new DateTime(2025, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var next = schedule.RecalculateNextRun(baseTime);

        Assert.Null(next);
        Assert.Null(schedule.NextRunAt);
    }

    [Fact]
    public async Task TestSuiteScheduleTask_Executes_DeployerCalled_WhenWithinTolerance()
    {
        var deployer = Substitute.For<ITestSuiteDeployer>();
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(ITestSuiteDeployer)).Returns(deployer);

        var logger = Substitute.For<ILogger<object>>();

        var task = new MongoTestSuiteScheduleTask
        {
            EntityId = "suite-1",
            Environment = "dev",
            Cpu = 2,
            Memory = 512,
            Profile = "p"
        };

        var nextRunAt = DateTime.UtcNow; // within tolerance


        await task.ExecuteAsync(services, nextRunAt, logger, CancellationToken.None);

        await deployer.Received(1).DeployAsync(
            "suite-1",
            "dev",
            2,
            512,
            "p",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task TestSuiteScheduleTask_DoesNotExecute_DeployerNotCalled_WhenOutsideTolerance()
    {
        var deployer = Substitute.For<ITestSuiteDeployer>();
        var services = Substitute.For<IServiceProvider>();
        services.GetService(typeof(ITestSuiteDeployer)).Returns(deployer);

        var logger = Substitute.For<ILogger<object>>();

        var task = new MongoTestSuiteScheduleTask
        {
            EntityId = "suite-1",
            Environment = "dev",
            Cpu = 2,
            Memory = 512,
            Profile = "p"
        };

        var nextRunAt = DateTime.UtcNow.AddMinutes(-10); // outside tolerance

        await task.ExecuteAsync(services, nextRunAt, logger, CancellationToken.None);

        await deployer.DidNotReceiveWithAnyArgs().DeployAsync(default!, default!, default, default, default, default);
    }
}