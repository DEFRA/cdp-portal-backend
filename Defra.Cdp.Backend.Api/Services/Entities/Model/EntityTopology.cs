using System.Text.Json.Serialization;

namespace Defra.Cdp.Backend.Api.Services.Entities.Model;


public record TopologyResourceLink(string? Service, string? Resource, string? Type, string? Access)
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ResourceRequestId { get; set; } = null;

    public string GetName() {
        return $"{Service}-{Resource}-{Type}-{Access}";
    }
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
        }).ToList();

        var newServices = secondary.FindAll(sec => primary.Find(prime => prime.Name == sec.Name) == null).ToList();

        return [.. existingServices, .. newServices];
    }

    /*
     * Combine TopologyResources based on Name. primary takes precedence over a match on secondary
     */
    private static List<TopologyResource> Combine(List<TopologyResource> primary, List<TopologyResource> secondary)
    {
        var existingResources = primary.Select(prime =>
        {
            var matchingResource = secondary.Find(sec => sec.Name == prime.Name);
            return matchingResource == null ? prime : new TopologyResource(prime.Name, prime.Type, prime.Icon, Combine(prime.Links ?? [], matchingResource.Links ?? []));
        }).ToList();

        var newResources = secondary.FindAll(sec => primary.Find(prime => prime.Name == sec.Name) == null).ToList();

        return [.. existingResources, .. newResources];
    }

    /*
     * Combine TopologyResourceLinks based on GetName. primary takes precedence over a match on secondary
     */
    private static List<TopologyResourceLink> Combine(List<TopologyResourceLink> primary, List<TopologyResourceLink> secondary)
    {
        return [.. primary, .. Deduplicate(secondary, primary)];
    }

    private static List<TopologyResourceLink> Deduplicate(List<TopologyResourceLink> items, List<TopologyResourceLink> existing) {
        return items.FindAll(item => existing.Find(e => e.GetName() == item.GetName()) == null);
    }
}