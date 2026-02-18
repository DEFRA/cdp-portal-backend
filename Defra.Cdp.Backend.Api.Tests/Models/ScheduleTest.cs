using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;

namespace Defra.Cdp.Backend.Api.Tests.Models;

public class ScheduleTests
{
    private readonly JsonSerializerOptions _options = new() { PropertyNameCaseInsensitive = true };

    [Fact]
    public void CanDeserializeScheduleRequest_WithOnceConfig()
    {
        var runAt = DateTime.UtcNow.AddHours(1);

        var json = $$"""
                     
                             {
                               "teamId": "team-1",
                               "enabled": true,
                               "task": {
                                 "type": "DeployTestSuite",
                                 "testSuite": "smoke",
                                 "environment": "dev",
                                 "cpu": 256,
                                 "memory": 512,
                                 "profile": "default"
                               },
                               "config": {
                                 "frequency": "ONCE",
                                 "runAt": "{{runAt:O}}"
                               }
                             }
                     """;

        var result = JsonSerializer.Deserialize<ScheduleRequest>(json, _options);

        Assert.NotNull(result);
        Assert.Equal("team-1", result!.TeamId);

        var task = Assert.IsType<TestSuiteScheduleTask>(result.Task);
        Assert.Equal("smoke", task.TestSuite);
        Assert.Equal("dev", task.Environment);
        Assert.Equal(256, task.Cpu);
        Assert.Equal(512, task.Memory);

        var config = Assert.IsType<OnceConfig>(result.Config);
        Assert.Equal(runAt, config.RunAt);

        var cron = config.GetCronExpression();
        Assert.Contains($"{runAt.Minute} {runAt.Hour}", cron);
    }

    [Fact]
    public void OnceConfig_Validate_Fails_WhenInPast()
    {
        var config = new OnceConfig { RunAt = DateTime.UtcNow.AddMinutes(-5), Frequency = "ONCE" };

        var context = new ValidationContext(config);
        var results = config.Validate(context).ToList();

        Assert.Single(results);
        Assert.Contains("future", results[0].ErrorMessage);
    }

    [Fact]
    public void DailyRecurringConfig_GeneratesCorrectCron()
    {
        var config = new DailyRecurringConfig { Frequency = "DAILY", Time = "14:30" };

        var cron = config.GetCronExpression();

        Assert.Equal("30 14 * * *", cron);
    }

    [Fact]
    public void WeeklyRecurringConfig_GeneratesCorrectCron()
    {
        var config = new WeeklyRecurringConfig
        {
            Frequency = "WEEKLY", Time = "09:15", DaysOfWeek = ["Monday", "Friday"]
        };

        var cron = config.GetCronExpression();

        // Monday = 1, Friday = 5
        Assert.Equal("15 9 * * 1,5", cron);
    }

    [Fact]
    public void IntervalRecurringConfig_GeneratesCorrectCron_ForHours()
    {
        var config = new IntervalRecurringConfig
        {
            Frequency = "INTERVAL", Every = new Interval { Value = 2, Unit = IntervalUnit.Hours }
        };

        var cron = config.GetCronExpression();

        Assert.Equal("0 */2 * * *", cron);
    }

    [Fact]
    public void CronRecurringConfig_ReturnsExpressionDirectly()
    {
        var config = new CronRecurringConfig { Frequency = "CRON", Expression = "*/5 * * * *" };

        var cron = config.GetCronExpression();

        Assert.Equal("*/5 * * * *", cron);
    }

    [Fact]
    public void ScheduleConfigConverter_Throws_WhenFrequencyMissing()
    {
        const string json = """
                            {
                              "teamId": "team-1",
                              "enabled": true,
                              "task": {
                                "type": "DeployTestSuite",
                                "testSuite": "smoke",
                                "environment": "dev",
                                "cpu": 256,
                                "memory": 512
                              },
                              "config": {}
                            }
                            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ScheduleRequest>(json, _options));
    }
}