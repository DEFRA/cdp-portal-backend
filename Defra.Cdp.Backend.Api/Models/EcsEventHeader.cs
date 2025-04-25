using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Models;

// Used to extract message type from ECS message so we can re-parse it with the correct parser.
public class EcsEventHeader
{
    [property: JsonPropertyName("id")] public string? Id { get; set; }
    public string DetailType { get; set; } = "";
    
    [JsonPropertyName("detailType")]
    public string DetailTypeCamel
    {
        set => DetailType = value;
    }

    [JsonPropertyName("detail-type")]
    public string DetailTypeKebab
    {
        set => DetailType = value;
    }

}