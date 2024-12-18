namespace Defra.Cdp.Backend.Api.Config;

public class GitHubWorkflowEventListenerOptions
{
    public const string Prefix = "GitHubWorkflowEvents";
    public string QueueUrl { get; set; } = null!;
    public bool Enabled { get; set; }
}