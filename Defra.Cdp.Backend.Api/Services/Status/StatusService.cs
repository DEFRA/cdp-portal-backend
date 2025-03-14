using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.GitHubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;

namespace Defra.Cdp.Backend.Api.Services.Status;

public interface IStatusService
{
    public Task<Status> GetTenantStatus(string service, CancellationToken cancellationToken = default);
}

public class StatusService(
    IDeployableArtifactsService deployableArtifactsService,
    ISquidProxyConfigService squidProxyConfigService,
    ITenantServicesService tenantService,
    IRepositoryService repositoryService,
    ISecretsService secretsService
    ) : IStatusService
{
    public async Task<Status> GetTenantStatus(string service, CancellationToken cancellationToken = default)
    {
        var images = await deployableArtifactsService.FindAllTagsForRepo(service, cancellationToken);
        var secrets = await secretsService.FindAllServiceSecrets(service, cancellationToken);
        var github = await repositoryService.FindRepositoryById(service, cancellationToken);
        var squid = await squidProxyConfigService.FindSquidProxyConfig(service, cancellationToken);
        var tenant = await tenantService.Find(new TenantServiceFilter(name: service), cancellationToken);

        var status = new Status
        {
            Name = service,
            ImageCount = images.Count,
            Github = github != null,
            Secrets = secrets.Count > 0,
            Squid = squid.Count > 0,
            TenantService = tenant.Count > 0
        };
        return status;
    }
}