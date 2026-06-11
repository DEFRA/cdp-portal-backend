using System.Text;
using System.Text.Json;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

namespace Defra.Cdp.Backend.Api.Services.Create;

public interface ICreateResourceWorkflowService
{
    Task<ResourceRequestRecord> CreateResources(CreateTenantResourceRequest request, UserDetails user,
        CancellationToken cancellationToken);
}

public class CreateResourceWorkflowWorkflowService(IHttpClientFactory clientFactory, 
    IGithubCredentialAndConnectionFactory githubCredentialAndConnectionFactory,
    IResourceRequestService resourceRequestService,
    IConfiguration configuration, 
    ILogger<CreateResourceWorkflowWorkflowService> logger) : ICreateResourceWorkflowService
{
    private readonly string _githubApiUrl = $"{configuration.GetValue<string>("Github:ApiUrl")!}";
    private readonly string _githubOrg =  $"{configuration.GetValue<string>("Github:Organisation")!}";
    
    private readonly string _configRepo = "cdp-tenant-config";
    private readonly string _cdpWorkflowId = "generic-cdp-cli-workflow.yml";

        
    public async Task<ResourceRequestRecord> CreateResources(CreateTenantResourceRequest request, UserDetails user, CancellationToken cancellationToken)
    {
        var runId = Guid.NewGuid().ToString();
        var branch = $"tenant-request-{runId}";
        var title = PrTitleBuilder.Build(request);
        var inputs = request.ToWorkflowInputs(runId, branch, title);
        
        var response = await TriggerWorkflow(inputs, cancellationToken);
        var name = request.GetServices().FirstOrDefault("no service");
        return await resourceRequestService.RecordRequest(name, user, request, inputs, response, cancellationToken);
    }
    
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
        
        logger.LogInformation("Requesting resources via {Workflow} with payload {Payload}", _cdpWorkflowId, jsonPayload);
        logger.LogInformation("Trigger GitHub {Workflow} responded with {Status}", _cdpWorkflowId, response.StatusCode);
        
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubTriggerWorkflowResponse>(cancellationToken: cancellationToken);
    }


}
