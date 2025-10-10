using Amazon.CloudWatch.EMF.Logger;
using Amazon.CloudWatch.EMF.Model;
using Defra.Cdp.Backend.Api.Services.Audit;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public class CloudWatchMetricsService
{
    public static void RecordMetric(string metricName, IDictionary<string, string> dimensions,
        ILogger<AuditService> logger)
    {
        logger.LogInformation("Recording metric {metricName}", metricName);
        using var metricsLogger = new MetricsLogger();
        metricsLogger.SetNamespace("cdp-portal-backend");
        var dimensionSet = new DimensionSet();
        foreach (var dimension in dimensions)
        {
            dimensionSet.AddDimension(dimension.Key, dimension.Value);
        }
        metricsLogger.SetDimensions(dimensionSet);
        metricsLogger.PutMetric(metricName, 1, Unit.COUNT);
        metricsLogger.Flush();
        logger.LogInformation("Recorded metric {metricName} & flushed", metricName);
    }
}