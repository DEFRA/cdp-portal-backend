using Defra.Cdp.Backend.Api.Mongo;
using Quartz;

namespace Defra.Cdp.Backend.Api.Services.Usage;

public class StatsScheduler(IMongoLock mongoLock, IUsageStatsService usageStatsService, ILogger<StatsScheduler> logger)
    : IJob
{
    public async Task Execute(IJobExecutionContext context)
    {
        try
        {
            if (!await mongoLock.Lock("statsReporter", TimeSpan.FromMinutes(5), context.CancellationToken)) return;
            await usageStatsService.ReportStats(context.CancellationToken);
            logger.LogInformation("reported stats to cloudwatch");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish stats");
        }
        finally
        {
            await mongoLock.Unlock("statsReporter", context.CancellationToken);
        }
    }
}