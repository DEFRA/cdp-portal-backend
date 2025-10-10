using Amazon.CloudWatch;
using Amazon.CloudWatch.Model;

namespace Defra.Cdp.Backend.Api.Services.Aws;

public interface ICloudWatchMetricsService
{
    Task IncrementAsync(string metricName, double amount = 1, IDictionary<string, string>? dimensions = null,
        DateTime? timestamp = null, CancellationToken ct = default);
}

public class CloudWatchMetricsService(
    IAmazonCloudWatch cloudWatch,
    ILoggerFactory loggerFactory) : ICloudWatchMetricsService
{
    private readonly ILogger<CloudWatchMetricsService> _logger = loggerFactory.CreateLogger<CloudWatchMetricsService>();

    private async Task PutMetricAsync(string metricName, double value, StandardUnit unit,
        IDictionary<string, string>? dimensions = null, DateTime? timestamp = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Putting metric {MetricName} with value {Value} and unit {Unit}", metricName, value, unit);
        if (string.IsNullOrWhiteSpace(metricName))
        {
            _logger.LogWarning("Metric name is null or whitespace. Metric data will not be sent to CloudWatch.");
            return;
        }

        var started = DateTime.UtcNow;
        const int timeoutSeconds = 8;
        
        _logger.LogInformation(
            "CloudWatch client config: Region={Region}, ServiceURL={ServiceURL}, Timeout={ClientTimeoutMs}ms, ProxyHost={ProxyHost}, ProxyPort={ProxyPort}",
            cloudWatch.Config.RegionEndpoint?.SystemName,
            cloudWatch.Config.ServiceURL,
            cloudWatch.Config.Timeout?.TotalMilliseconds,
            cloudWatch.Config.ProxyHost,
            cloudWatch.Config.ProxyPort);

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

            _logger.LogInformation(
                "Sending metric to CloudWatch. MetricName: {MetricName}, Value: {Value}, Unit: {Unit}, Dimensions: {@Dimensions}",
                metricName, value, unit, dimensions);

            var request = new PutMetricDataRequest
            {
                MetricData = [datum],
                Namespace = "cdp-portal-backend"
            };

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

            await cloudWatch.PutMetricDataAsync(request, timeoutCts.Token);

            var elapsedTotal = (DateTime.UtcNow - started).TotalMilliseconds;

            _logger.LogInformation(
                "Successfully sent metric to CloudWatch in completed in {ElapsedTotal}ms. MetricName: {MetricName}, Value: {Value}, Unit: {Unit}, Dimensions: {@Dimensions}",
                elapsedTotal, metricName, value, unit, dimensions);
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            var elapsed = (DateTime.UtcNow - started).TotalSeconds;
            _logger.LogWarning(
                "Timed out sending metric to CloudWatch after {ElapsedSeconds}s. Treating as non-fatal and continuing.",
                elapsed);
        }
        catch (AmazonCloudWatchException ace)
        {
            var elapsed = (DateTime.UtcNow - started).TotalMilliseconds;
            _logger.LogError(ace,
                "CloudWatch API error after {ElapsedMs}ms. StatusCode={StatusCode}, ErrorCode={ErrorCode}", elapsed,
                ace.StatusCode, ace.ErrorCode);
        }
        catch (HttpRequestException hre)
        {
            var elapsed = (DateTime.UtcNow - started).TotalMilliseconds;
            _logger.LogError(hre,
                "HTTP failure talking to CloudWatch after {ElapsedMs}ms. Check VPC endpoints, egress, DNS, proxy.",
                elapsed);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to put metric data to CloudWatch for metric {MetricName}", metricName);
        }
    }

    public Task IncrementAsync(string metricName, double amount = 1, IDictionary<string, string>? dimensions = null,
        DateTime? timestamp = null, CancellationToken ct = default)
        => PutMetricAsync(metricName, amount, StandardUnit.Count, dimensions, timestamp, ct);
}