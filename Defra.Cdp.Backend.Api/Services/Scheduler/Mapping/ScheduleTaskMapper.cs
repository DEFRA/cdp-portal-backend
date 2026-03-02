using Defra.Cdp.Backend.Api.Models.Schedules;
using Defra.Cdp.Backend.Api.Services.scheduler.Model;

namespace Defra.Cdp.Backend.Api.Services.scheduler.Mapping;

public static class ScheduleTaskMapper
{
    public static MongoScheduleTask ToMongo(ScheduleTask task)
    {
        MongoScheduleTask mongoTask = task switch
        {
            TestSuiteTask ts => new MongoTestSuiteScheduleTask
            {
                EntityId = ts.EntityId,
                Environment = ts.Environment,
                Cpu = ts.Cpu,
                Memory = ts.Memory,
                Profile = ts.Profile
            },
            // add other task types here...
            _ => throw new NotSupportedException($"Unknown task type {task.GetType()}")
        };
        return mongoTask;
    }
    
    public static MongoScheduleTask ToMongo(EntityScheduleTask task, string entityId)
    {
        MongoScheduleTask mongoTask = task switch
        {
            EntityTestSuiteTask ts => new MongoTestSuiteScheduleTask
            {
                EntityId = entityId,
                Environment = ts.Environment,
                Cpu = ts.Cpu,
                Memory = ts.Memory,
                Profile = ts.Profile
            },
            // add other task types here...
            _ => throw new NotSupportedException($"Unknown task type {task.GetType()}")
        };
        return mongoTask;
    }
    
}