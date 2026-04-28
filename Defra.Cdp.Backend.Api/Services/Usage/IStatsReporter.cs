using Defra.Cdp.Backend.Api.Services.Aws;

namespace Defra.Cdp.Backend.Api.Services.Usage;

public interface IStatsReporter
{
    Task ReportStats(ICloudWatchMetricsService metrics, CancellationToken cancellationToken);
}