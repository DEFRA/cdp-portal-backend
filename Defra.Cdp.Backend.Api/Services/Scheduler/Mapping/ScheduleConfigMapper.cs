using Defra.Cdp.Backend.Api.Models.Schedules;
using Defra.Cdp.Backend.Api.Services.Scheduler.Model;

namespace Defra.Cdp.Backend.Api.Services.scheduler.Mapping;

public static class ScheduleConfigMapper
{
    public static MongoScheduleConfig ToMongo(ScheduleConfig scheduleConfig)
    {
        MongoScheduleConfig config = scheduleConfig switch
        {
            OnceConfig c => new MongoOnceConfig { RunAt = c.RunAt },
            DailyRecurringConfig c => new MongoDailyRecurringConfig { Time = c.Time, },
            WeeklyRecurringConfig c => new MongoWeeklyRecurringConfig { Time = c.Time, DaysOfWeek = c.DaysOfWeek, },
            IntervalRecurringConfig c => new MongoIntervalRecurringConfig
            {
                Every = new MongoInterval { Unit = c.Every.Unit.ToString(), Value = c.Every.Value, }
            },
            CronRecurringConfig c => new MongoCronRecurringConfig { Expression = c.Expression },
            _ => throw new ArgumentOutOfRangeException(nameof(scheduleConfig), scheduleConfig, null)
        };

        config.StartDate = scheduleConfig.StartDate;
        config.EndDate = scheduleConfig.EndDate;
        config.Frequency = scheduleConfig.Frequency;
        return config;
    }
}