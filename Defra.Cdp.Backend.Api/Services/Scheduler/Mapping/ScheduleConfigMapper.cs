using Defra.Cdp.Backend.Api.Models.Schedules;
using Defra.Cdp.Backend.Api.Services.Scheduler.Model;

namespace Defra.Cdp.Backend.Api.Services.Scheduler.Mapping;

public static class ScheduleConfigMapper
{
    public static MongoScheduleConfig ToMongo(ScheduleConfig scheduleConfig)
    {
        MongoScheduleConfig config = scheduleConfig switch
        {
            OnceConfig c => new MongoOnceConfig
            {
                RunAt = ScheduleTimezone.ToUtc(c.RunAt, c.Timezone),
                Timezone = c.Timezone
                
            },
            DailyRecurringConfig c => new MongoDailyRecurringConfig { Time = c.Time, Timezone = c.Timezone },
            WeeklyRecurringConfig c => new MongoWeeklyRecurringConfig { Time = c.Time, DaysOfWeek = c.DaysOfWeek, Timezone = c.Timezone },
            IntervalRecurringConfig c => new MongoIntervalRecurringConfig
            {
                Every = new MongoInterval { Unit = c.Every.Unit.ToString(), Value = c.Every.Value },
                Timezone = c.Timezone
            },
            CronRecurringConfig c => new MongoCronRecurringConfig { Expression = c.Expression, Timezone = c.Timezone },
            _ => throw new ArgumentOutOfRangeException(nameof(scheduleConfig), scheduleConfig, null)
        };

        var timezone = scheduleConfig.Timezone;

        config.StartDate = ScheduleTimezone.ToUtc(scheduleConfig.StartDate, timezone);
        config.EndDate = scheduleConfig.EndDate.HasValue
            ? ScheduleTimezone.ToUtc(scheduleConfig.EndDate.Value, timezone)
            : null;
        config.Frequency = scheduleConfig.Frequency;
        return config;
    }
}