using Defra.Cdp.Backend.Api.Services.Aws;

namespace Defra.Cdp.Backend.Api.Services.Usage;

public interface IUsageStatsService
{
    Task ReportStats(CancellationToken cancellationToken);
}

public class UsageStatsService(ICloudWatchMetricsService metrics, IEnumerable<IStatsReporter> reporters)
    : IUsageStatsService
{
    public async Task ReportStats(CancellationToken cancellationToken)
    {
        foreach (var statsReporter in reporters)
        {
            await statsReporter.ReportStats(metrics, cancellationToken);
        }
    }
}