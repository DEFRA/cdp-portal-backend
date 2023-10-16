using System.Net.Http.Headers;
using System.Text.Json;
using GitHubJwt;
using Octokit.GraphQL;
using ProductHeaderValue = Octokit.GraphQL.ProductHeaderValue;

namespace Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

public class GithubCredentialAndConnectionFactory : ICredentialStore
{
    private readonly int _appInstallationId;
    private readonly GitHubJwtFactory _generator;
    private readonly string _githubOrgName;
    private readonly DateTimeOffset _lastConnectionRenewal = DateTimeOffset.MinValue;
    private Connection _githubConnection = null!;
    private DateTimeOffset _lastTokenGeneratedTime = DateTimeOffset.MinValue;
    private string _latestJwt = null!;

    public GithubCredentialAndConnectionFactory(IConfiguration configuration)
    {
        var encodedPem = configuration.GetValue<string>("Github:AppKey")!;
        var keySource = new Base64StringPrivateKeySource(encodedPem);
        var appId = configuration.GetValue<int>("Github:AppId")!;
        _appInstallationId = configuration.GetValue<int>("Github:AppInstallationId");
        _githubOrgName = configuration.GetValue<string>("Github:Organisation")!;
        _generator = new GitHubJwtFactory(
            keySource,
            new GitHubJwtFactoryOptions
            {
                // AppIntegrationId = 38398116, // The GitHub App Id
                AppIntegrationId = appId, // The GitHub App Id
                ExpirationSeconds = 600 // 10 minutes is the maximum time allowed
            }
        );
    }


    public Task<string> GetCredentials(CancellationToken cancellationToken = new())
    {
        if (DateTimeOffset.Now - _lastTokenGeneratedTime < TimeSpan.FromMinutes(10)) return Task.FromResult(_latestJwt);

        _latestJwt = _generator.CreateEncodedJwtToken();
        _lastTokenGeneratedTime = DateTimeOffset.Now;
        return Task.FromResult(_latestJwt);
    }

    // https://docs.github.com/en/apps/creating-github-apps/authenticating-with-a-github-app/generating-an-installation-access-token-for-a-github-app
    //curl --request POST \
    // --url "https://api.github.com/app/installations/<appInstallationId>/access_tokens" \
    // --header "Accept: application/vnd.github+json" \
    // --header "Authorization: Bearer <token>" \
    // --header "X-GitHub-Api-Version: 2022-11-28"
    public async Task<Connection> GetGithubConnection(CancellationToken cancellationToken = new())
    {
        if (DateTimeOffset.Now - _lastConnectionRenewal < TimeSpan.FromHours(1))
            return _githubConnection;

        var token = await GetCredentials(cancellationToken);
        using HttpClient client = new();
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        client.DefaultRequestHeaders.Add(
            "Accept", "application/vnd.github+json");
        client.DefaultRequestHeaders.Add("X-GitHub-Api-Version", "2022-11-28");
        client.DefaultRequestHeaders.Add("User-Agent",
            "cdp-portal-backend"); // required by Github API or else you get 403

        // This call is encapsulated in the non-graphql library. To avoid depending on both restful and graphql 
        // libraries, I'm choosing to call this URL directly. You can see the equivalent curl command in the comment
        // above the method
        var uri = $"https://api.github.com/app/installations/{_appInstallationId}/access_tokens";
        var result =
            await client.PostAsync(uri,
                null, cancellationToken);

        result.EnsureSuccessStatusCode();
        var responseBodyStream = await result.Content.ReadAsStreamAsync(cancellationToken);
        var appInstallationResult =
            await JsonSerializer.DeserializeAsync<AppInstallationResult>(responseBodyStream,
                cancellationToken: cancellationToken);
        _githubConnection =
            new Connection(new ProductHeaderValue(_githubOrgName), appInstallationResult?.Token);
        return _githubConnection;
    }

    // We just want the installation token
    private record AppInstallationResult(string Token);
}