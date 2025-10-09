using Amazon.CloudWatch.EMF.Logger;
using Amazon.CloudWatch.EMF.Model;
using Defra.Cdp.Backend.Api.Config;
using Microsoft.Extensions.Options;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public interface ICloudWatchMetricsService
{
    void RecordCount(string metricName, IDictionary<string, string>? dimensions = null, double value = 1);
}

public class CloudWatchMetricsService(
    ILogger<CloudWatchMetricsService> logger,
    IOptions<CloudWatchMetricsOptions> options)
    : ICloudWatchMetricsService
{
    private readonly CloudWatchMetricsOptions _options = options.Value;

    public void RecordCount(string metricName, IDictionary<string, string>? dimensions = null, double value = 1)
    {
        if (string.IsNullOrWhiteSpace(metricName))
        {
            logger.LogWarning("Attempted to record metric with empty name");
            return;
        }

        try
        {
            using var metricsLogger = new MetricsLogger();
            metricsLogger.SetNamespace(_options.Namespace);

            if (dimensions is { Count: > 0 })
            {
                var dimensionSet = new DimensionSet();
                var added = 0;
                foreach (var kvp in dimensions)
                {
                    if (string.IsNullOrWhiteSpace(kvp.Key) || string.IsNullOrWhiteSpace(kvp.Value)) continue;

                    if (added >= _options.MaxDimensions)
                    {
                        logger.LogWarning(
                            "Truncating dimensions for {MetricName} to {MaxDimensions}",
                            metricName, _options.MaxDimensions);
                        break;
                    }

                    dimensionSet.AddDimension(kvp.Key, kvp.Value);
                    added++;
                }

                if (added > 0)
                {
                    metricsLogger.SetDimensions(dimensionSet);
                }
            }

            metricsLogger.PutMetric(metricName, value, Unit.COUNT);
            logger.LogInformation(
                "Recorded metric {MetricName} value {Value} with {DimCount} dimensions",
                metricName, value, dimensions?.Count ?? 0);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to record metric {MetricName}", metricName);
        }
    }
}