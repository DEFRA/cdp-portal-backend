namespace Defra.Cdp.Backend.Api.Services.Entities.Model;


public record TopologyResourceLink(string? Service, string? Resource, string Type);

public record TopologyResource(string Name, string Icon, List<TopologyResourceLink>? Links);

public record TopologyService(string Name, SubType? Type, List<Team> Teams, List<TopologyResource> Resources);
