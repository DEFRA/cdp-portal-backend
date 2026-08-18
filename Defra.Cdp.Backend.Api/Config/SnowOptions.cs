namespace Defra.Cdp.Backend.Api.Config;

public class SnowOptions
{
    public const string Prefix = "Snow";

    /// <summary>
    ///     Workflow file in cdp-deployments-snow that the deployment record is dispatched to. Overridden per
    ///     environment via the Snow__Workflow environment variable (e.g. infra-dev.yml for testing).
    /// </summary>
    public const string DefaultWorkflow = "deploy.yml";

    public string[] TriggerEnvironments { get; set; } = [];
    public string Workflow { get; set; } = DefaultWorkflow;
}
