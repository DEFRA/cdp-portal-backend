using System.Text;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

namespace Defra.Cdp.Backend.Api.Services.Github.Workflows;

public interface ITriggerWorkflowService
{
    public Task<GitHubTriggerWorkflowResponse?> TriggerWorkflow(string repo, string workflow,
        IGithubWorkflowInputs inputs, CancellationToken cancellationToken);
}

public class TriggerWorkflowService(IHttpClientFactory clientFactory, 
    IGithubCredentialAndConnectionFactory githubCredentialAndConnectionFactory,
    IConfiguration configuration, 
    ILogger<TriggerWorkflowService> logger) : ITriggerWorkflowService
{
    private readonly string _githubApiUrl = $"{configuration.GetValue<string>("Github:ApiUrl")!}";
    private readonly string _githubOrg =  $"{configuration.GetValue<string>("Github:Organisation")!}";
    
    public async Task<GitHubTriggerWorkflowResponse?> TriggerWorkflow(string repo, string workflow, IGithubWorkflowInputs inputs, CancellationToken cancellationToken)
    {
        var url = $"{_githubApiUrl}/repos/{_githubOrg}/{repo}/actions/workflows/{workflow}/dispatches";
        var client = clientFactory.CreateClient("GitHubClient");

        var payload = new
        {
            @ref = "main",
            inputs = inputs
        };

        var jsonPayload = JsonSerializer.Serialize(payload);
        using var request = new HttpRequestMessage(HttpMethod.Post, url);
        var token = await githubCredentialAndConnectionFactory.GetToken(cancellationToken);
        
        request.Headers.Add("Authorization", $"Bearer {token}");
        request.Headers.Add("Accept", "application/vnd.github+json");
        request.Headers.Add("User-Agent", "CdpPortalBackend");
        request.Headers.Add("X-GitHub-Api-Version", "2026-03-10");
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        logger.LogInformation("Triggering workflow {Workflow} in repo {Repo} with payload {Payload}", workflow, repo, jsonPayload);

        var response = await client.SendAsync(request, cancellationToken);
        logger.LogInformation("Trigger GitHub {Workflow} responded with {Status}", workflow, response.StatusCode);
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubTriggerWorkflowResponse>(cancellationToken: cancellationToken);
    }
}