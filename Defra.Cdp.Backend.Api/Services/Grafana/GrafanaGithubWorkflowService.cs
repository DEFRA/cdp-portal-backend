using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github.Workflows;
using Defra.Cdp.Backend.Api.Services.Grafana.Models;

namespace Defra.Cdp.Backend.Api.Services.Grafana;

public interface IGrafanaGithubWorkflowService
{
    Task<PromotionRequestRecord> PromoteDashboard(DashboardPromotionRequest dashboard, UserDetails? user, CancellationToken cancellationToken);
    Task<PromotionRequestRecord> PromoteAllAlerts(AlertPromotionRequest alert, UserDetails? user, CancellationToken cancellationToken);
    Task<PromotionRequestRecord> PromoteAlert(AlertPromotionRequest alert, UserDetails? user, CancellationToken cancellationToken);

}

public class GrafanaGithubWorkflowService(ITriggerWorkflowService triggerWorkflowService, IGrafanaPromotionRequestService promotionRequestService) : IGrafanaGithubWorkflowService
{
    
    private const string Repo = "cdp-grafana-svc";
    private const string DashboardPromotionWorkflow = "promote-custom-dashboard.yml";
    private const string AllAlertPromotionWorkflow = "promote-advanced-alerts.yml";
    private const string SingleAlertPromotionWorkflow = "promote-advanced-alert.yml";
    
    public async Task<PromotionRequestRecord> PromoteDashboard(DashboardPromotionRequest dashboard, UserDetails? user, CancellationToken cancellationToken)
    {
        var response = await triggerWorkflowService.TriggerWorkflow(Repo, DashboardPromotionWorkflow, dashboard,
            cancellationToken);
        var result = await promotionRequestService.RecordRequest(user, dashboard, response, cancellationToken);
        return result;
    }
    
    public async Task<PromotionRequestRecord> PromoteAllAlerts(AlertPromotionRequest alert, UserDetails? user, CancellationToken cancellationToken)
    {
        var response = await triggerWorkflowService.TriggerWorkflow(Repo, AllAlertPromotionWorkflow, alert,
            cancellationToken);
        var result = await promotionRequestService.RecordRequest(user, alert, response, cancellationToken);
        return result;
    }
    
    public async Task<PromotionRequestRecord> PromoteAlert(AlertPromotionRequest alert, UserDetails? user, CancellationToken cancellationToken)
    {
        var response = await triggerWorkflowService.TriggerWorkflow(Repo, SingleAlertPromotionWorkflow, alert,
            cancellationToken);
        var result = await promotionRequestService.RecordRequest(user, alert, response, cancellationToken);
        return result;
    }
}