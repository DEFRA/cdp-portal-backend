namespace Defra.Cdp.Backend.Api.Services.Aws;

using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;

public interface ICloudWatchMetricsService
{
    Task IncrementAsync(string metricName, double amount = 1, IDictionary<string, string>? dimensions = null, DateTime? timestamp = null, CancellationToken ct = default);
}

public class CloudWatchMetricsService(
    IAmazonCloudWatch cloudWatch,
    ILogger<CloudWatchMetricsService> logger) : ICloudWatchMetricsService
{
    private async Task PutMetricAsync(string metricName, double value, StandardUnit unit,
        IDictionary<string, string>? dimensions = null, DateTime? timestamp = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(metricName)) return;

        try
        {
            var datum = new MetricDatum
            {
                MetricName = metricName,
                Unit = unit,
                Value = value,
                Dimensions = dimensions?.Select(kv => new Dimension { Name = kv.Key, Value = kv.Value }).ToList(),
                TimestampUtc = timestamp ?? DateTime.UtcNow
            };

            var request = new PutMetricDataRequest { MetricData = [datum] };

            await cloudWatch.PutMetricDataAsync(request, ct);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to put metric data to CloudWatch for metric {MetricName}", metricName);
        }
    }

    public Task IncrementAsync(string metricName, double amount = 1, IDictionary<string, string>? dimensions = null, DateTime? timestamp = null, CancellationToken ct = default)
        => PutMetricAsync(metricName, amount, StandardUnit.Count, dimensions, timestamp, ct);
}