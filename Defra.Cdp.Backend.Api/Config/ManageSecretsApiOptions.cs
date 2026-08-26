namespace Defra.Cdp.Backend.Api.Config;

public class ManageSecretsApiOptions
{
    public const string Prefix = "ManageSecretsApi";

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
    /// Only required when BaseUrlTemplate contains a {restApiId} placeholder.
    /// </summary>
    public Dictionary<string, string> RestApiIds { get; init; } = new();
}
