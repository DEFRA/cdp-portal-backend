using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Entities.Model;


public record TopologyResourceLink(string? Service, string? Resource, string? Type, string? Access)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResourceRequestId { get; set; } = null;
};

public record TopologyResource(string Name, string Type, string Icon, List<TopologyResourceLink>? Links)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResourceRequestId { get; set; } = null;
};

public record TopologyService(string Name,
    [property: JsonConverter(typeof(JsonStringEnumConverter<SubType>))]
    SubType? Type,
    List<Team> Teams,
    List<TopologyResource> Resources
);