namespace Defra.Cdp.Backend.Api.Models;

public class EcrEventListenerOptions
{
    public const string Prefix = "EcrEvents";

    public string QueueUrl { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int WaitTimeSeconds { get; set; } = 15;
}

public record SqsEcrEventDetail(
    string Result,
    string RepositoryName,
    string ImageTag,
    string ActionType
);

public record SqsEcrEvent(
    SqsEcrEventDetail Detail,
    string DetailType
);