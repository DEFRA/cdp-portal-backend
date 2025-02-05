namespace Defra.Cdp.Backend.Api.Config;

public class PlatformEventListenerOptions
{
   public const string Prefix = "PlatformPortalEvents";
   public string QueueUrl { get; set; } = null!;
   public bool Enabled { get; set; }
}
