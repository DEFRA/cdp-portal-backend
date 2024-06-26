namespace Defra.Cdp.Backend.Api.Config;

public class SecretEventListenerOptions
{
    public const string Prefix = "SecretManagerEvents";
    public string QueueUrl { get; set; } = null!;
    public bool Enabled { get; set; }
}