namespace Defra.Cdp.Backend.Api.Services.Aws;

using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;

public interface ICloudWatchMetricsService
{
    Task IncrementAsync(string metricName, double amount = 1, IDictionary<string, string>? dimensions = null,
        DateTime? timestamp = null, CancellationToken ct = default);
}

public class CloudWatchMetricsService(
    IAmazonCloudWatch cloudWatch,
    ILogger<CloudWatchMetricsService> logger) : ICloudWatchMetricsService
{
    private async Task PutMetricAsync(string metricName, double value, StandardUnit unit,
        IDictionary<string, string>? dimensions = null, DateTime? timestamp = null, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(metricName))
        {
            logger.LogWarning("Metric name is null or whitespace. Metric data will not be sent to CloudWatch.");
            return;
        }

        var started = DateTime.UtcNow;
        var timeoutSeconds = 8;

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

            logger.LogInformation(
                "Sending metric to CloudWatch. MetricName: {MetricName}, Value: {Value}, Unit: {Unit}, Dimensions: {@Dimensions}",
                metricName, value, unit, dimensions);

            var request = new PutMetricDataRequest { MetricData = [datum] };

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));
            
            await cloudWatch.PutMetricDataAsync(request, timeoutCts.Token);

            var elapsedTotal = (DateTime.UtcNow - started).TotalMilliseconds;

            logger.LogInformation(
                "Successfully sent metric to CloudWatch in completed in {ElapsedTotal}ms. MetricName: {MetricName}, Value: {Value}, Unit: {Unit}, Dimensions: {@Dimensions}",
                elapsedTotal, metricName, value, unit, dimensions);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            var elapsed = (DateTime.UtcNow - started).TotalSeconds;
            logger.LogWarning(
                "Timed out sending metric to CloudWatch after {ElapsedSeconds}s. Treating as non-fatal and continuing.",
                elapsed);
        }
        catch (AmazonCloudWatchException ace)
        {
            var elapsed = (DateTime.UtcNow - started).TotalMilliseconds;
            logger.LogError(ace,
                "CloudWatch API error after {ElapsedMs}ms. StatusCode={StatusCode}, ErrorCode={ErrorCode}", elapsed,
                ace.StatusCode, ace.ErrorCode);
        }
        catch (HttpRequestException hre)
        {
            var elapsed = (DateTime.UtcNow - started).TotalMilliseconds;
            logger.LogError(hre,
                "HTTP failure talking to CloudWatch after {ElapsedMs}ms. Check VPC endpoints, egress, DNS, proxy.",
                elapsed);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to put metric data to CloudWatch for metric {MetricName}", metricName);
        }
    }

    public Task IncrementAsync(string metricName, double amount = 1, IDictionary<string, string>? dimensions = null,
        DateTime? timestamp = null, CancellationToken ct = default)
        => PutMetricAsync(metricName, amount, StandardUnit.Count, dimensions, timestamp, ct);
}