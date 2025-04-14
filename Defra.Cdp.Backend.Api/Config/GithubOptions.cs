namespace Defra.Cdp.Backend.Api.Config;


public class GithubOptions
{
    public const string Prefix = "Github";
    
    public string Organisation { get; set; }
    public GithubWorkflowsOptions Workflows { get; set; }
    public GithubReposOptions Repos { get; set; }
}
public class GithubWorkflowsOptions
{
    public string CreateAppConfig { get; set; }
    public string CreateNginxUpstreams { get; set; }
    public string CreateSquidConfig { get; set; }
    public string CreateDashboard { get; set; }
    public string CreateMicroservice { get; set; }
    public string CreateRepository { get; set; }
    public string CreateJourneyTestSuite { get; set; }
    public string CreatePerfTestSuite { get; set; }
    public string CreateTenantService { get; set; }
    public string ApplyTenantService { get; set; }
    public string ManualApplyTenantService { get; set; }
    public string NotifyPortal { get; set; }
}

public class GithubReposOptions
{
    public string CdpTfSvcInfra { get; set; }
    public string CdpAppConfig { get; set; }
    public string CdpNginxUpstreams { get; set; }
    public string CdpSquidProxy { get; set; }
    public string CdpGrafanaSvc { get; set; }
    public string CdpCreateWorkflows { get; set; }
    public string CdpAppDeployments { get; set; }
}