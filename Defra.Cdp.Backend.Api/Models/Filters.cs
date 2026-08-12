namespace Defra.Cdp.Backend.Api.Models;

public record TeamFilter(string? TeamId, string? Name);

public sealed class DeploymentFilters
{
    public List<string> Services { get; init; } = default!;
    public List<string> Statuses { get; init; } = default!;
    public List<UserDetails> Users { get; init; } = default!;
    public List<TeamFilter> Teams { get; set; } = default!;
    public List<string> Kinds { get; set; } = default!;
}

public sealed class ServiceFilters
{
    public List<string> Services { get; init; } = default!;
    public List<TeamFilter> Teams { get; set; } = default!;
}