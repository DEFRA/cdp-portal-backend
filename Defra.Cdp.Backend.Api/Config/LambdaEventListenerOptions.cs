namespace Defra.Cdp.Backend.Api.Config;

public class LambdaEventListenerOptions
{
    public const string Prefix = "LambdaEvents";
    public string QueueUrl { get; init; } = null!;
    public bool Enabled { get; init; }
}