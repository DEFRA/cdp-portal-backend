namespace Defra.Cdp.Backend.Api.Config;

/// <summary>
/// Controls which CDP environments trigger the ServiceNow deployment record workflow.
/// </summary>
/// <remarks>
/// Defaults to empty (opt-in only, via config) because cdp-portal-backend is a single shared instance
/// handling deployment events for every CDP environment, not one instance per environment.
///
/// Must default to an empty array, not e.g. ["prod"]: the configuration binder appends bound values onto a
/// non-empty default instead of replacing it, silently duplicating entries already set in config.
/// </remarks>
public class SnowOptions
{
    public const string Prefix = "Snow";
    public string[] TriggerEnvironments { get; set; } = [];
}
