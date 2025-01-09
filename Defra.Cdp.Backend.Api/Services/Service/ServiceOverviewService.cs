using Defra.Cdp.Backend.Api.Services.Deployments;
using Defra.Cdp.Backend.Api.Services.Github;
using Defra.Cdp.Backend.Api.Services.GithubWorkflowEvents.Services;
using Defra.Cdp.Backend.Api.Services.Secrets;
using Defra.Cdp.Backend.Api.Services.TenantArtifacts;
using Defra.Cdp.Backend.Api.Utils;
using Defra.Cdp.Backend.Api.Utils.Fetchers;

namespace Defra.Cdp.Backend.Api.Services.Service;

public interface IServiceOverviewService
{
    Task<ServiceV2?> GetService(string name, CancellationToken cancellationToken = default);
}

public class ServiceOverviewService(
    IDeployableArtifactsService deployableArtifactsService,
    ISquidProxyConfigService squidProxyConfigService,
    ITenantServicesService tenantService,
    IRepositoryService repositoryService,
    ISecretsService secretsService,
    IVanityUrlService vanityUrlService,
    IDeploymentsServiceV2 deploymentsService,
    SelfServiceOpsFetcher selfServiceOpsFetcher
    ) : IServiceOverviewService
{
    public async Task<ServiceV2?> GetService(string name, CancellationToken cancellationToken = default)
    {
        var service = new ServiceV2
        {
            Name = name,
            TenantService = await tenantService.FindAllServices(name, cancellationToken),
            CreationStatus = await selfServiceOpsFetcher.FindStatus(name),
            Deployments = await deploymentsService.FindWhatsRunningWhere(name, cancellationToken),
            LatestBuilds = await deployableArtifactsService.FindLatestTagsForRepo(name, 6, cancellationToken),
            VanityUrls = await vanityUrlService.FindService(name, cancellationToken),
            SquidProxyConfig = await squidProxyConfigService.FindSquidProxyConfig(name, cancellationToken),
            Secrets = await secretsService.FindAllSecrets(name, cancellationToken),
            Github = await repositoryService.FindRepositoryById(name, cancellationToken)
        };
        
        
        if (service.Github != null)
        {
            service.Teams = service.Github.Teams.ToList();
        }
        
        var activeEnvironments = service.Deployments.Select(d => d.Environment).ToList();
        activeEnvironments.Sort(new EnvironmentComparer());
        
        // Build service links
        service.Logs = activeEnvironments.Select(e =>
            new ServiceUrl{ 
                Environment = e,
                Name = $"https://logs.{e}.defra.cloud",
                Url = $"https://logs.{e}.cdp-int.defra.cloud/_dashboards/app/dashboards#/view/89f63d50-b6eb-11ee-a385-15667195f827?_g=(filters:!(),refreshInterval:(pause:!t,value:0),time:(from:now-15m,to:now))&_a=(description:'',filters:!(('$state':(store:appState),meta:(alias:!n,controlledBy:'1705679462997',disabled:!f,index:e55f3890-5d4a-11ee-8f40-670c9b0b8093,key:container_name,negate:!f,params:(query:cdp-portal-frontend),type:phrase),query:(match_phrase:(container_name:{name})))),fullScreenMode:!f,options:(hidePanelTitles:!f,useMargins:!t),query:(language:kuery,query:''),timeRestore:!f,title:'CDP%20Service%20Dashboard',viewMode:view)"
            }
        ).ToList();

        service.Metrics = activeEnvironments.Select(e => 
            new ServiceUrl{
                Environment = e,
                Name = $"https://logs.{e}.defra.cloud",
                Url = $"https://metrics.{e}.cdp-int.defra.cloud/d/{name}/{name}-service"
            }).ToList();

        service.InternalUrls = activeEnvironments.Select(e => 
            new ServiceUrl{
                Environment = e,
                Name = $"https://{name}.{e}.cdp-int.defra.cloud",
                Url = $"https://{name}.{e}.cdp-int.defra.cloud"
            }).ToList();
        
        if (service.TenantService.Count > 0) {
            service.DockerHub = new ServiceUrl
            {
                Name = $"https://hub.docker.com/r/defradigital/{name}/tags",
                Url = $"https://hub.docker.com/r/defradigital/{name}/tags"
            };
        }

        return service;
    }
}