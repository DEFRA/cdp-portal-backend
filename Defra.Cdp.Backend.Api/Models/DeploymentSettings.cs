namespace Defra.Cdp.Backend.Api.Models;

public sealed class DeploymentSettings
{
    public string? Memory { get; init; }
    public string? Cpu { get; init; }
    public int? InstanceCount { get; init; }
}