namespace Defra.Cdp.Backend.Api.Config;

public class MonoLambdaOptions
{
    public const string Prefix = "LambdaEvents";
    public string QueueUrl { get; init; } = null!;
    public string? TopicArn { get; init; }
    public bool Enabled { get; init; }
}