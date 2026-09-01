namespace Defra.Cdp.Backend.Api.Config;

/// <summary>
/// Shared config for the mono-lambda private API Gateway, used by all sync mono-lambda clients
/// (<see cref="Services.Secrets.ManageSecretsClient"/>, <see cref="Services.Grafana.GrafanaPlaygroundsClient"/>, ...) -
/// same API, different paths, so one REST API id lookup per environment rather than one per feature.
/// </summary>
public class MonoLambdaApiOptions
{
    public const string Prefix = "MonoLambdaApi";

    /// <summary>
    /// URL template for the mono-lambda API Gateway. Supports two placeholders:
    /// {environment} - the tenant environment being targeted (also used as the API Gateway stage name).
    /// {restApiId} - the API Gateway REST API id for that environment, looked up from <see cref="RestApiIds"/>.
    /// Each tenant environment has its own separate mono-lambda API Gateway (own account, own REST API id,
    /// single stage matching that environment's name), so both placeholders must come from the same
    /// target environment's entry.
    /// </summary>
    public string BaseUrlTemplate { get; init; } = null!;

    /// <summary>
    /// Maps a tenant environment name to the mono-lambda API Gateway's REST API id in that environment's account.
    /// A list rather than a Dictionary&lt;string, string&gt; because cdp-app-config's env var keys can only
    /// contain letters, digits and underscores - environment names like "infra-dev" aren't valid dictionary
    /// keys there. Only required when BaseUrlTemplate contains a {restApiId} placeholder.
    /// </summary>
    public List<RestApiIdMapping> RestApiIds { get; init; } = new();
}

public sealed record RestApiIdMapping(string Environment, string RestApiId);
