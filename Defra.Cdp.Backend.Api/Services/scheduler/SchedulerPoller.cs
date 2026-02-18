using Defra.Cdp.Backend.Api.Mongo;
using MongoDB.Driver;
using Quartz;

namespace Defra.Cdp.Backend.Api.Services.scheduler;

public class SchedulerPoller(
    ILoggerFactory loggerFactory,
    ISchedulerService schedulerService,
    IMongoLock mongoLock,
    IServiceProvider serviceProvider
)
    : IJob
{
    private readonly ILogger<SchedulerPoller> _logger =
        loggerFactory.CreateLogger<SchedulerPoller>();

    private const string LockName = "processScheduledTasks";

    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            if (!await mongoLock.Lock(LockName, TimeSpan.FromSeconds(60)))
            {
                return;
            }

            var ct = context.CancellationToken;
            var now = DateTime.UtcNow;
            var tolerance = TimeSpan.FromMinutes(5);

            var dueSchedules = await schedulerService.FetchDueSchedules(ct);

            foreach (var schedule in dueSchedules)
            {
                _logger.LogInformation("Processing schedule {id} for team {teamId} and description {description}",
                    schedule.Id, schedule.TeamId, schedule.Description);
                var shouldExecute =
                    schedule.NextRunAt.HasValue &&
                    schedule.NextRunAt.Value >= now - tolerance;

                using var scope = serviceProvider.CreateScope();

                if (shouldExecute)
                {
                    try
                    {
                        await schedule.Task.ExecuteAsync(scope.ServiceProvider, schedule.NextRunAt, _logger, ct);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex,
                            "Error executing schedule {id} for team {team} with schedule {description}", schedule.Id,
                            schedule.TeamId, schedule.Description);
                        // this will cause us to retry up to 5 times
                        continue;
                    }
                }

                // Add a minute so NextRunAt for one-off and minute frequencies get calculated correctly 
                schedule.RecalculateNextRun(now.AddMinutes(1));

                await schedulerService.UpdateAsync(schedule.Id,
                    Builders<Schedule>.Update.Set(s => s.NextRunAt, schedule.NextRunAt), ct);
            }
        }
        finally
        {
            await mongoLock.Unlock(LockName);
        }
    }
}