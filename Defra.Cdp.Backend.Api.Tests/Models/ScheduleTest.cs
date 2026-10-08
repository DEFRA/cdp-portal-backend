using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Models.Schedules;
using Defra.Cdp.Backend.Api.Services.Scheduler.Mapping;
using Defra.Cdp.Backend.Api.Services.Scheduler.Model;

namespace Defra.Cdp.Backend.Api.Tests.Models;

public class ScheduleTests
{
    private readonly JsonSerializerOptions _options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [Fact]
    public void CanDeserializeScheduleRequest_WithOnceConfig()
    {
        var runAt = DateTime.UtcNow.AddHours(1);

        var json = $$"""
                             {
                               "enabled": true,
                               "task": {
                                 "type": "DeployTestSuite",
                                 "entityId":"my-test-entity",
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

        var task = Assert.IsType<TestSuiteTask>(result.Task);
        Assert.Equal("my-test-entity", task.EntityId);
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
        var config = new OnceConfig
        {
            RunAt = DateTime.UtcNow.AddMinutes(-5),
            Frequency = "ONCE"
        };

        var context = new ValidationContext(config);
        var results = config.Validate(context).ToList();

        Assert.Single(results);
        Assert.Contains("future", results[0].ErrorMessage);
    }

    [Fact]
    public void OnceConfig_Validate_Passes_WhenInFuture()
    {
        var config = new OnceConfig
        {
            RunAt = DateTime.UtcNow.AddMinutes(5),
            Frequency = "ONCE",
            Timezone = "UTC"
        };

        var context = new ValidationContext(config);
        var results = config.Validate(context).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void ScheduleConfig_Validate_Passes_WithValidTimezone()
    {
        var config = new DailyRecurringConfig
        {
            Frequency = "DAILY",
            Time = "08:00",
            Timezone = "Europe/London"
        };

        var context = new ValidationContext(config);
        var results = config.Validate(context).ToList();

        Assert.Empty(results);
    }

    [Fact]
    public void ScheduleConfig_Validate_Fails_WithInvalidTimezone()
    {
        var config = new DailyRecurringConfig
        {
            Frequency = "DAILY",
            Time = "08:00",
            Timezone = "Not/A/Timezone"
        };

        var context = new ValidationContext(config);
        var results = config.Validate(context).ToList();

        Assert.Single(results);
        Assert.Contains("Invalid IANA timezone", results[0].ErrorMessage);
    }

    [Fact]
    public void ScheduleConfig_DefaultsToEuropeLondon()
    {
        var config = new DailyRecurringConfig
        {
            Frequency = "DAILY",
            Time = "08:00"
        };

        Assert.Equal("Europe/London", config.Timezone);
    }

    [Fact]
    public void DailyRecurringConfig_GeneratesCorrectCron()
    {
        var config = new DailyRecurringConfig
        {
            Frequency = "DAILY",
            Time = "14:30"
        };

        var cron = config.GetCronExpression();

        Assert.Equal("30 14 * * *", cron);
    }

    [Fact]
    public void WeeklyRecurringConfig_GeneratesCorrectCron()
    {
        var config = new WeeklyRecurringConfig
        {
            Frequency = "WEEKLY",
            Time = "09:15",
            DaysOfWeek = ["Monday", "Friday"]
        };

        var cron = config.GetCronExpression();

        Assert.Equal("15 9 * * 1,5", cron);
    }

    [Theory]
    [InlineData(5, IntervalUnit.Minutes, "*/5 * * * *")]
    [InlineData(2, IntervalUnit.Hours, "0 */2 * * *")]
    [InlineData(3, IntervalUnit.Days, "0 0 */3 * *")]
    public void IntervalRecurringConfig_GeneratesCorrectCron(
        int value,
        IntervalUnit unit,
        string expectedCron)
    {
        var config = new IntervalRecurringConfig
        {
            Frequency = "INTERVAL",
            Every = new Interval
            {
                Value = value,
                Unit = unit
            }
        };

        var cron = config.GetCronExpression();

        Assert.Equal(expectedCron, cron);
    }

    [Fact]
    public void CronRecurringConfig_ReturnsExpressionDirectly()
    {
        var config = new CronRecurringConfig
        {
            Frequency = "CRON",
            Expression = "*/5 * * * *"
        };

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

    [Fact]
    public void ScheduleConfigConverter_Throws_WhenFrequencyUnknown()
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
                              "config": {
                                "frequency": "INVALID"
                              }
                            }
                            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ScheduleRequest>(json, _options));
    }

    [Fact]
    public void OnceConfig_Throws_WhenUnknownPropertyProvided()
    {
        const string json = """
                            {
                              "frequency": "ONCE",
                              "runAt": "2027-01-01T10:00:00",
                              "somethingElse": "unexpected"
                            }
                            """;

        Assert.Throws<JsonException>(() =>
            JsonSerializer.Deserialize<ScheduleConfig>(json, _options));
    }

    [Fact]
    public void ScheduleConfigMapper_ConvertsLondonGmtTimeToUtc()
    {
        var config = new OnceConfig
        {
            Frequency = "ONCE",
            Timezone = "Europe/London",
            RunAt = new DateTime(2026, 2, 19, 11, 20, 0)
        };

        var mongo = ScheduleConfigMapper.ToMongo(config);

        var once = Assert.IsType<MongoOnceConfig>(mongo);

        Assert.Equal(
            new DateTime(2026, 2, 19, 11, 20, 0, DateTimeKind.Utc),
            once.RunAt);
    }

    [Fact]
    public void ScheduleConfigMapper_ConvertsLondonBstTimeToUtc()
    {
        var config = new OnceConfig
        {
            Frequency = "ONCE",
            Timezone = "Europe/London",
            RunAt = new DateTime(2026, 7, 19, 11, 20, 0)
        };

        var mongo = ScheduleConfigMapper.ToMongo(config);

        var once = Assert.IsType<MongoOnceConfig>(mongo);

        Assert.Equal(
            new DateTime(2026, 7, 19, 10, 20, 0, DateTimeKind.Utc),
            once.RunAt);
    }

    [Fact]
    public void ScheduleConfigMapper_ConvertsStartDateAndEndDateToUtc()
    {
        var config = new DailyRecurringConfig
        {
            Frequency = "DAILY",
            Time = "08:00",
            Timezone = "Europe/London",
            StartDate = new DateTime(2026, 7, 19, 11, 20, 0),
            EndDate = new DateTime(2026, 7, 20, 11, 20, 0)
        };

        var mongo = ScheduleConfigMapper.ToMongo(config);

        Assert.Equal(
            new DateTime(2026, 7, 19, 10, 20, 0, DateTimeKind.Utc),
            mongo.StartDate);

        Assert.Equal(
            new DateTime(2026, 7, 20, 10, 20, 0, DateTimeKind.Utc),
            mongo.EndDate);
    }

    [Fact]
    public void ScheduleConfigMapper_PreservesNullEndDate()
    {
        var config = new DailyRecurringConfig
        {
            Frequency = "DAILY",
            Time = "08:00",
            Timezone = "Europe/London",
            EndDate = null
        };

        var mongo = ScheduleConfigMapper.ToMongo(config);

        Assert.Null(mongo.EndDate);
    }

    [Fact]
    public void ScheduleConfigMapper_PreservesTimezone()
    {
        var config = new DailyRecurringConfig
        {
            Frequency = "DAILY",
            Time = "08:00",
            Timezone = "Europe/London"
        };

        var mongo = ScheduleConfigMapper.ToMongo(config);

        Assert.Equal("Europe/London", mongo.Timezone);
    }
}