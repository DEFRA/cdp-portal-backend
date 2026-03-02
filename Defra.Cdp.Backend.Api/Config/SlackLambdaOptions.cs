namespace Defra.Cdp.Backend.Api.Config;

public sealed class SlackLambdaOptions
{
    public const string Prefix = "SlackLambda";
    public string? TopicArn { get; set; }
    public bool Enabled { get; set; }
}