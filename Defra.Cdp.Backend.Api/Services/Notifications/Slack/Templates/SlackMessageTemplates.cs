using System.Text.Json;
using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;


public sealed class SlackMessageBody
{
    public List<Block>? Blocks { get; init; }
    public string? Text { get; init; }
}

public sealed class Block
{
    [property: JsonPropertyName("type")] public string? Type { get; init; }
    [property: JsonPropertyName("text")] public TextObject? Text { get; init; }
    [property: JsonPropertyName("fields")] public List<TextObject>? Fields { get; init; }
    [property: JsonPropertyName("elements")] public List<TextObject>? Elements { get; init; }
}

public sealed class TextObject
{
    [property: JsonPropertyName("type")] public string? Type { get; init; }
    [property: JsonPropertyName("text")] public string? Text { get; init; }
    [property: JsonPropertyName("emoji")] public bool? Emoji { get; init; }
}

public static partial class SlackMessageTemplates
{
    private static string EscapeMarkdown(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}