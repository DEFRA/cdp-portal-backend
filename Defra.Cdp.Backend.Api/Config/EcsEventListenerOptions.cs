namespace Defra.Cdp.Backend.Api.Config;

public class EcsEventListenerOptions
{
    public const string Prefix = "EcsEvents";
    public string QueueUrl { get; set; } = null!;
    public bool Enabled { get; set; }
}