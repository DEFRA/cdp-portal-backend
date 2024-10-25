namespace Defra.Cdp.Backend.Api.Config;

public class ActionEventListenerOptions
{
    public const string Prefix = "ActionEvents";
    public string QueueUrl { get; set; } = null!;
    public bool Enabled { get; set; }
}