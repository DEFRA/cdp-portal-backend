namespace Defra.Cdp.Backend.Api.Config;

public class EcsEventListenerOptions
{
    public const string Prefix = "EcsEvents";
    public string QueueUrl { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public List<string> ContainerToIgnore { get; set; } = new();
}