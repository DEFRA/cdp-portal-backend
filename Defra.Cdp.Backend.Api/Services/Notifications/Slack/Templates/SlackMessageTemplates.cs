using System.Text.Json;
using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;


public sealed class SlackMessageTemplate
{
    public List<Block>? Blocks { get; set; }
    public string? Text { get; set; }
}

public sealed class Block
{
    public string? Type { get; set; }
    public TextObject? Text { get; set; }
    public List<TextObject>? Fields { get; set; }
    public List<TextObject>? Elements { get; set; }
}

public sealed class TextObject
{
    public string? Type { get; set; }
    public string? Text { get; set; }
    public bool? Emoji { get; set; }
}

public static partial class SlackMessageTemplates
{
    private static readonly JsonSerializerOptions s_jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string EscapeMarkdown(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}