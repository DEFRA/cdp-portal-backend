using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Github.ScheduledTasks;

namespace Defra.Cdp.Backend.Api.Services.Create;

public interface ICreateWorkflowRequest
{
    GenericCdpWorkflowInputs BuildWorkflowInput(string? runId, string? useBranch, string? prTitle);
}

public class GenericCdpWorkflowInputs(List<string> commands, string? runId, string? useBranch, string? prTitle)
{
    [JsonPropertyName("run_id")]
    public string? RunId { get; init; } = runId;

    [JsonPropertyName("commands")]
    public string Commands { get; init; } = JsonSerializer.Serialize(commands);

    [JsonPropertyName("use_branch")]
    public string? UseBranch { get; init; } = useBranch;

    [JsonPropertyName("pr_title")]
    public string? PrTitle { get; init; } = prTitle;
}

public class CreateResource(IHttpClientFactory clientFactory, IGithubCredentialAndConnectionFactory githubCredentialAndConnectionFactory, IConfiguration configuration)
{
    private readonly string _githubApiUrl = $"{configuration.GetValue<string>("Github:ApiUrl")!}/graphql";

    private readonly string _githubOrg = "DEFRA";
    private readonly string _configRepo = "cdp-tenant-config";
    private readonly string _cdpWorkflowId = "generic-cdp-cli-workflow.yml";
    
    public async Task TriggerWorkflow(GenericCdpWorkflowInputs inputs, CancellationToken cancellationToken)
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
        request.Headers.Add("X-GitHub-Api-Version", "2022-11-28");
        request.Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json");

        var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}