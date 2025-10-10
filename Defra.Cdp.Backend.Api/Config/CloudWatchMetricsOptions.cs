namespace Defra.Cdp.Backend.Api.Config;

public sealed class CloudWatchMetricsOptions
{
    public const string Prefix = "CloudWatch";
    public string Namespace { get; set; } = "cdp-portal-backend";
    public int MaxDimensions { get; set; } = 29;
}