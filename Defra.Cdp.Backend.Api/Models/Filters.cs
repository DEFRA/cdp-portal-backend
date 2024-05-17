namespace Defra.Cdp.Backend.Api.Models;

public sealed class DeploymentFilters
{
    public List<string> Services { get; init; } = default!;
    public List<string> Statuses { get; init; } = default!;
    public List<UserDetails> Users { get; init; } = default!;
}