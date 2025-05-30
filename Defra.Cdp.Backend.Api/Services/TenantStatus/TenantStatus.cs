namespace Defra.Cdp.Backend.Api.Services.TenantStatus;

public class TenantStatus
{
    public required string Name { get; set; }
    public int ImageCount { get; set; }
    public bool Squid { get; set; }
    public bool TenantService { get; set; }
    public bool Github { get; set; }
    public bool Secrets { get; set; }
}