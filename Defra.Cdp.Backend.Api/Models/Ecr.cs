using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

public class EcrEventListenerOptions
{
    public const string Prefix = "EcrEvents";

    public string QueueUrl { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int WaitTimeSeconds { get; set; } = 15;
}

public record SqsEcrEventDetail(
    [property: JsonPropertyName("result")] string Result,
    [property: JsonPropertyName("repository-name")]
    string RepositoryName,
    [property: JsonPropertyName("image-tag")]
    string ImageTag,
    [property: JsonPropertyName("action-type")]
    string ActionType,
    [property: JsonPropertyName("image-digest")]
    string ImageDigest
);

public sealed record SqsEcrEvent(
    [property: JsonPropertyName("detail")] SqsEcrEventDetail Detail,
    [property: JsonPropertyName("detail-type")]
    string DetailType
);

public sealed record EcrEventCopy(string MessageId, DateTimeOffset? CreatedAt, string Body);

public class ImageProcessingException : Exception
{
    public ImageProcessingException(string? message) : base(message)
    {
    }
}