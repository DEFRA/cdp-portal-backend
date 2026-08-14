namespace Defra.Cdp.Backend.Api.Config;

/// <summary>
/// Controls which CDP environments trigger the ServiceNow deployment record workflow.
/// </summary>
/// <remarks>
/// <para>
/// cdp-portal-backend runs as a single shared instance that processes deployment events for every
/// CDP environment (see EnvironmentMappings), so this can't be set per-environment via ASPNETCORE_ENVIRONMENT
/// appsettings files alone for anything other than local runs. The real default (["prod"]) lives in
/// appsettings.json; override Snow__TriggerEnvironments (e.g. via a CDP_ prefixed environment variable) to
/// temporarily include another environment such as infra-dev for end-to-end testing.
/// </para>
/// <para>
/// TriggerEnvironments must default to an empty array here, not e.g. ["prod"]. The configuration binder
/// appends bound values onto an existing non-empty array/list default instead of replacing it, so a
/// non-empty default here would silently duplicate entries already set in appsettings.json.
/// </para>
/// </remarks>
public class SnowOptions
{
    public const string Prefix = "Snow";
    public string[] TriggerEnvironments { get; set; } = [];
}
