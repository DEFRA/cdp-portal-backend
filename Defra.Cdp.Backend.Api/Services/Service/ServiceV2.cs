using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Utils.Clients;

namespace Defra.Cdp.Backend.Api.Services.Service;

public class ServiceV2
{
    public required string Name { get; set; }
    public Repository? Github { get; set; }
    public List<VanityUrlRecord> VanityUrls { get; set; } = [];
    public List<RepositoryTeam> Teams { get; set; } = [];
    public List<TagInfo> LatestBuilds { get; set; } = [];
    public List<DeploymentV2> Deployments { get; set; } = [];
    public List<TenantServiceRecord>? TenantService { get; set; }
    public List<ServiceUrl> Metrics { get; set; } = [];
    public List<ServiceUrl> Logs { get; set; }  = [];
    public List<ServiceUrl> InternalUrls { get; set; } = [];
    public ServiceUrl? DockerHub { get; set; }
    public CreationStatus? CreationStatus { get; set; }
    public List<SquidProxyConfigRecord> SquidProxyConfig { get; set; } = [];
    public Dictionary<string, TenantSecretKeys> Secrets { get; set; } = [];

    public bool IsEmpty()
    {
        return Github == null &&
               VanityUrls.Count == 0 &&
               LatestBuilds.Count == 0 &&
               Deployments.Count == 0 &&
               (TenantService == null || TenantService.Count == 0) &&
               CreationStatus == null &&
               SquidProxyConfig.Count == 0 &&
               Secrets.Count == 0;
    }
}

public class ServiceUrl {
    public required string Name { get; init; }
    public required string Url { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Environment { get; init; }
}
