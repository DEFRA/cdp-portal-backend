namespace Defra.Cdp.Backend.Api.Config;

public class GithubEventListenerOptions
{
    public const string Prefix = "GithubEvents";
    public string QueueUrl { get; set; } = null!;
    public bool Enabled { get; set; }
}