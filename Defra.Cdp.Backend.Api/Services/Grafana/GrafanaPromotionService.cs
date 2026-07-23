using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github.Workflows;
using Defra.Cdp.Backend.Api.Services.Grafana.Models;

namespace Defra.Cdp.Backend.Api.Services.Grafana;

public interface IGrafanaPromotionService
{
    Task<PromotionRequestRecord> PromoteDashboard(DashboardPromotionRequest dashboard, UserDetails? user, CancellationToken cancellationToken);
    Task<PromotionRequestRecord> PromoteAlerts(AlertPromotionRequest alert, UserDetails? user, CancellationToken cancellationToken);
}

public class GrafanaPromotionService(ITriggerWorkflowService triggerWorkflowService, IGrafanaPromotionRequestService promotionRequestService, IGrafanaPlaygroundService playgroundService) : IGrafanaPromotionService
{
    
    private const string Repo = "cdp-grafana-modules";
    private const string DashboardPromotionWorkflow = "promote-custom-dashboard.yml";
    private const string AlertPromotionWorkflow = "promote-advanaced-alerts.yml";
    
    public async Task<PromotionRequestRecord> PromoteDashboard(DashboardPromotionRequest dashboard, UserDetails? user, CancellationToken cancellationToken)
    {
        var response = await triggerWorkflowService.TriggerWorkflow(Repo, DashboardPromotionWorkflow, dashboard,
            cancellationToken);
        var result = await promotionRequestService.RecordRequest(user, dashboard, response, cancellationToken);
        return result;
    }
    
    public async Task<PromotionRequestRecord> PromoteAlerts(AlertPromotionRequest alert, UserDetails? user, CancellationToken cancellationToken)
    {
        var response = await triggerWorkflowService.TriggerWorkflow(Repo, AlertPromotionWorkflow, alert,
            cancellationToken);
        var result = await promotionRequestService.RecordRequest(user, alert, response, cancellationToken);
        return result;
    }
}