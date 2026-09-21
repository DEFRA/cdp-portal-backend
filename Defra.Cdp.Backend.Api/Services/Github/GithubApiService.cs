using System.Net;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

namespace Defra.Cdp.Backend.Api.Services.Github;

public interface IGithubApiService
{
    Task<bool> DoesRepoExist(string repo,
        CancellationToken cancellationToken = default);
}

public class GithubApiService(
    IHttpClientFactory clientFactory,
    IGithubCredentialAndConnectionFactory githubCredentialAndConnectionFactory,
    IConfiguration configuration,
    ILogger<GithubApiService> logger) : IGithubApiService
{
    private readonly string _githubApiUrl = $"{configuration.GetValue<string>("Github:ApiUrl")!}";
    private readonly string _githubOrg = Uri.EscapeDataString($"{configuration.GetValue<string>("Github:Organisation")!}");
    
    public async Task<bool> DoesRepoExist(string repo,
        CancellationToken cancellationToken = default)
    {
        var uriBuilder = new UriBuilder(_githubApiUrl) { Path = $"/repos/{_githubOrg}/{Uri.EscapeDataString(repo)}" };
        var client = clientFactory.CreateClient("GitHubClient");
       
        using var request = new HttpRequestMessage(HttpMethod.Get, uriBuilder.Uri);
        var token = await githubCredentialAndConnectionFactory.GetToken(cancellationToken);

        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Headers.Add("Accept", "application/vnd.github+json");
        request.Headers.Add("User-Agent", "CdpPortalBackend");
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");

        var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var exists = response.IsSuccessStatusCode;
        logger.LogInformation("Checking if GitHub repo {repo} exists, exists: {exists}", repo, exists);
        if (response.StatusCode is not (HttpStatusCode.OK or HttpStatusCode.NotFound))
        {
            logger.LogWarning("Unexpected response code when checking {repo}: status: {code}", repo, response.StatusCode);
        }
        
        await response.Content.CopyToAsync(Stream.Null, cancellationToken);
        return exists;
    }
}
