namespace Defra.Cdp.Backend.Api.Config;

public class GithubWorkflowEventListenerOptions
{
    public const string Prefix = "GithubWorkflowEvents";
    public string QueueUrl { get; set; } = null!;
    public bool Enabled { get; set; }
}