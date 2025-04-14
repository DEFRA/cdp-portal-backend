using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;

namespace Defra.Cdp.Backend.Api.Services.TenantStatus;

public interface ITenantStatusService
{
    public Task<TenantStatus> GetTenantStatus(string service, CancellationToken cancellationToken = default);
}

public class TenantStatusService(
    IDeployableArtifactsService deployableArtifactsService,
    ISquidProxyConfigService squidProxyConfigService,
    ITenantServicesService tenantService,
    IRepositoryService repositoryService,
    ISecretsService secretsService
    ) : ITenantStatusService
{
    public async Task<TenantStatus> GetTenantStatus(string service, CancellationToken cancellationToken = default)
    {
        var images = await deployableArtifactsService.FindAllTagsForRepo(service, cancellationToken);
        var secrets = await secretsService.FindAllServiceSecrets(service, cancellationToken);
        var github = await repositoryService.FindRepositoryById(service, cancellationToken);
        var squid = await squidProxyConfigService.FindSquidProxyConfig(service, cancellationToken);
        var tenant = await tenantService.Find(new TenantServiceFilter{ Name = service }, cancellationToken);

        var status = new TenantStatus
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