using System.Text.Json;
using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Notifications.Slack.Templates;


public sealed class SlackMessageBody
{
    [JsonPropertyName("type")]
    public List<Block>? Blocks { get; init; }
    
    [JsonPropertyName("type")]
    public string? Text { get; init; }
}

public sealed class Block
{
    [JsonPropertyName("type")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; init; }
    
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public TextObject? Text { get; init; }
    
    
    [JsonPropertyName("fields")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TextObject>? Fields { get; init; }
    
    [property: JsonPropertyName("elements")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<TextObject>? Elements { get; init; }
}

public sealed class TextObject
{
    [JsonPropertyName("type")] 
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
    public string? Type { get; init; }
    
    [JsonPropertyName("text")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
    public string? Text { get; init; }
    
    [JsonPropertyName("emoji")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] 
    public bool? Emoji { get; init; }
}

public static partial class SlackMessageTemplates
{
    private static string EscapeMarkdown(string s) =>
        s.Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");
}