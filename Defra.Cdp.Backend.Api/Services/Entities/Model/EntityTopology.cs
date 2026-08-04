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


public static class TopologyServiceCombiner {

    /*
     * Combine TopologyServices based on TopologyResource Name. primary takes precedence over a match on secondary
     */
    public static List<TopologyService> Combine(List<TopologyService> primary, List<TopologyService> secondary)
    {
        var existingServices = primary.Select(prime =>
        {
            var matchingService = secondary.Find(sec => sec.Name == prime.Name);
            return matchingService == null ? prime : new TopologyService(prime.Name, prime.Type, prime.Teams, Combine(prime.Resources, matchingService.Resources));
        }).ToList() ?? [];

        var newServices = secondary.FindAll(sec => primary.Find(prime => prime.Name == sec.Name) == null).ToList() ?? [];

        return existingServices.Concat(newServices).ToList() ?? [];
    }

    /*
     * Combine TopologyResources based on Name. primary takes precedence over a match on secondary
     */
    private static List<TopologyResource> Combine(List<TopologyResource> primary, List<TopologyResource> secondary)
    {
        return primary.Concat(Deduplicate(secondary, primary)).ToList() ?? [];
    }

    private static List<TopologyResource> Deduplicate(List<TopologyResource> items, List<TopologyResource> existing) {
        return items.FindAll(item => existing.Find(e => e.Name == item.Name) == null);
    }
}