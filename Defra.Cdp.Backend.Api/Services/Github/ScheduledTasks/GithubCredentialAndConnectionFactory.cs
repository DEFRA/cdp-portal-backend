using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using GitHubJwt;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public interface IGithubCredentialAndConnectionFactory
{
    Task<string> GetCredentials(CancellationToken cancellationToken = new());
    Task<string?> GetToken(CancellationToken cancellationToken = new());
}

public class GithubCredentialAndConnectionFactory : IGithubCredentialAndConnectionFactory
{
    private readonly int _appInstallationId;
    private readonly HttpClient _client = new();
    private readonly GitHubJwtFactory _generator;
    private readonly string _githubApiUrl;
    private readonly DateTimeOffset _lastConnectionRenewal = DateTimeOffset.MinValue;
    private DateTimeOffset _lastTokenGeneratedTime = DateTimeOffset.MinValue;
    private string? _latestInstallationToken;
    private string _latestJwt = null!;

    public GithubCredentialAndConnectionFactory(IConfiguration configuration)
    {
        var encodedPem = configuration.GetValue<string>("Github:AppKey")!;
        var keySource = new Base64StringPrivateKeySource(encodedPem);
        _githubApiUrl = $"{configuration.GetValue<string>("Github:ApiUrl")!}";
        var appId = configuration.GetValue<int>("Github:AppId")!;
        _appInstallationId = configuration.GetValue<int>("Github:AppInstallationId");
        _generator = new GitHubJwtFactory(
            keySource,
            new GitHubJwtFactoryOptions
            {
                // AppIntegrationId = 38398116, // The GitHub App Id
                AppIntegrationId = appId, // The GitHub App Id
                ExpirationSeconds = 600 // 10 minutes is the maximum time allowed
            }
        );
        _client.DefaultRequestHeaders.Accept.Clear();
        _client.DefaultRequestHeaders.Add(
            "Accept", "application/vnd.github+json");
        _client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        _client.DefaultRequestHeaders.Add("User-Agent",
            "cdp-portal-backend"); // required by Github API or else you get 403
    }


    public Task<string> GetCredentials(CancellationToken cancellationToken = new())
    {
        if (DateTimeOffset.Now - _lastTokenGeneratedTime < TimeSpan.FromMinutes(10)) return Task.FromResult(_latestJwt);

        _latestJwt = _generator.CreateEncodedJwtToken();
        _lastTokenGeneratedTime = DateTimeOffset.Now;
        return Task.FromResult(_latestJwt);
    }

    public async Task<string?> GetToken(CancellationToken cancellationToken = new())
    {
        if (DateTimeOffset.Now - _lastConnectionRenewal < TimeSpan.FromHours(1))
            return _latestInstallationToken;

        var token = await GetCredentials(cancellationToken);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // This call is encapsulated in the non-graphql library. To avoid depending on both restful and graphql 
        // libraries, I'm choosing to call this URL directly. You can see the equivalent curl command in the comment
        // above the method
        var uri = $"{_githubApiUrl}/app/installations/{_appInstallationId}/access_tokens";
        var result =
            await _client.PostAsync(uri,
                null, cancellationToken);

        result.EnsureSuccessStatusCode();
        var responseBodyStream = await result.Content.ReadAsStreamAsync(cancellationToken);
        var appInstallationResult =
            await JsonSerializer.DeserializeAsync<AppInstallationResult>(responseBodyStream,
                cancellationToken: cancellationToken);
        _latestInstallationToken = appInstallationResult?.Token;
        return _latestInstallationToken;
    }

    // We just want the installation token
    private record AppInstallationResult([property: JsonPropertyName("token")] string Token);
}