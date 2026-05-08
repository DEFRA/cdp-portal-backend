using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

namespace Defra.Cdp.Backend.Api.Services.Create;

public interface ICreateResourceService
{
    Task<GitHubTriggerWorkflowResponse?> TriggerWorkflow(GenericCdpWorkflowInputs inputs, CancellationToken cancellationToken);
}

public class CreateResourceService(IHttpClientFactory clientFactory, IGithubCredentialAndConnectionFactory githubCredentialAndConnectionFactory, IConfiguration configuration, ILogger<CreateResourceService> logger) : ICreateResourceService
{
    private readonly string _githubApiUrl = $"{configuration.GetValue<string>("Github:ApiUrl")!}";
    private readonly string _githubOrg =  $"{configuration.GetValue<string>("Github:Organisation")!}";
    
    private readonly string _configRepo = "cdp-tenant-config";
    private readonly string _cdpWorkflowId = "generic-cdp-cli-workflow.yml";

    public async Task<GitHubTriggerWorkflowResponse?> TriggerWorkflow(GenericCdpWorkflowInputs inputs, CancellationToken cancellationToken)
    {
        var url = $"{_githubApiUrl}/repos/{_githubOrg}/{_configRepo}/actions/workflows/{_cdpWorkflowId}/dispatches";
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

        var response = await client.SendAsync(request, cancellationToken);
        
        logger.LogInformation("Trigger GitHub {Workflow} responded with {Status}", _cdpWorkflowId, response.StatusCode);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubTriggerWorkflowResponse>(cancellationToken: cancellationToken);
    }
}

public record GitHubTriggerWorkflowResponse
{
    [JsonPropertyName("workflow_run_id")]
    public long? WorkflowRunId { get; init; }

    [JsonPropertyName("run_url")]
    public string? WorkflowRunUrl { get; init; }
    
    [JsonPropertyName("html_url")]
    public string? WorkflowRunHtmlUrl { get; init; }
}
