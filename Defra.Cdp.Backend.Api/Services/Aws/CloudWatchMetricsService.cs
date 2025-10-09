using Amazon.CloudWatch.EMF.Logger;
using Amazon.CloudWatch.EMF.Model;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public class CloudWatchMetricsService
{
    public static void RecordMetric(string metricName, IDictionary<string, string> dimensions)
    {
        using var logger = new MetricsLogger();
        logger.SetNamespace("cdp-portal-backend");
        var dimensionSet = new DimensionSet();
        foreach (var dimension in dimensions)
        {
            dimensionSet.AddDimension(dimension.Key, dimension.Value);
        }
        logger.SetDimensions(dimensionSet);
        logger.PutMetric(metricName, 1, Unit.COUNT);
    }
}