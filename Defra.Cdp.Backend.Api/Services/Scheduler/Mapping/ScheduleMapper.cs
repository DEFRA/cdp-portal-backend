using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Models.Schedules;
using Defra.Cdp.Backend.Api.Services.Scheduler.Model;

namespace Defra.Cdp.Backend.Api.Services.Scheduler.Mapping;

public static class ScheduleMapper
{
    public static MongoSchedule ToMongo(EntityScheduleRequest schedule, UserDetails user, string entityId)
    {
        var task = ScheduleTaskMapper.ToMongo(schedule.Task, entityId);
        var config = ScheduleConfigMapper.ToMongo(schedule.Config);
        var mongoUser = new MongoUserDetails { Id = user.Id, DisplayName = user.DisplayName };
        return new MongoSchedule(
            schedule.Enabled,
            schedule.Config.GetCronExpression(),
            schedule.Config.GetDescription(),
            task,
            config,
            mongoUser
        );
    }

    public static MongoSchedule ToUpdatedMongo(EntityScheduleRequest schedule, MongoSchedule originalSchedule, UserDetails user, string entityId)
    {
        var task = ScheduleTaskMapper.ToMongo(schedule.Task, entityId);
        var config = ScheduleConfigMapper.ToMongo(schedule.Config);
        var mongoUser = new MongoUserDetails { Id = user.Id, DisplayName = user.DisplayName };
        var updatedSchedule = originalSchedule with
        {
            Enabled = schedule.Enabled,
            Cron = schedule.Config.GetCronExpression(),
            Description = schedule.Config.GetDescription(),
            Config = config,
            Task = task,
            User = mongoUser
        };

        updatedSchedule.RecalculateNextRun();
        return updatedSchedule;
    }

    public static MongoSchedule ToMongo(ScheduleRequest schedule, UserDetails user)
    {
        var task = ScheduleTaskMapper.ToMongo(schedule.Task);
        var config = ScheduleConfigMapper.ToMongo(schedule.Config);
        var mongoUser = new MongoUserDetails { Id = user.Id, DisplayName = user.DisplayName };
        return new MongoSchedule(
            schedule.Enabled,
            schedule.Config.GetCronExpression(),
            schedule.Config.GetDescription(),
            task,
            config,
            mongoUser
        );
    }

    public static MongoSchedule ToUpdatedMongo(ScheduleRequest schedule, MongoSchedule originalSchedule, UserDetails user, string entityId)
    {
        var task = ScheduleTaskMapper.ToMongo(schedule.Task);
        var config = ScheduleConfigMapper.ToMongo(schedule.Config);
        var mongoUser = new MongoUserDetails { Id = user.Id, DisplayName = user.DisplayName };
        var updatedSchedule = originalSchedule with
        {
            Enabled = schedule.Enabled,
            Cron = schedule.Config.GetCronExpression(),
            Description = schedule.Config.GetDescription(),
            Config = config,
            Task = task,
            User = mongoUser
        };

        updatedSchedule.RecalculateNextRun();
        return updatedSchedule;
    }

}