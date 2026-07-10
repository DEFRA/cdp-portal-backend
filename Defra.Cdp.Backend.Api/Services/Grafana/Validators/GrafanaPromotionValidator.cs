using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Grafana.Models;

namespace Defra.Cdp.Backend.Api.Services.Grafana.Validators;


public interface IGrafanaPromotionValidator
{
    Task<List<string>> Validate(GrafanaPromotionRequest request, CancellationToken cancellationToken);
}

public class GrafanaPromotionValidator(IEntityResourceService ers) : IGrafanaPromotionValidator
{
    public async Task<List<string>> Validate(GrafanaPromotionRequest request, CancellationToken cancellationToken)
    {
        List<string> errors = [];
        
        if (request.Dashboards.Count == 0 && request.Alerts.Count == 0)
        {
                errors.Add("The request contained no resources to promote");
        }
        
        if (request.Alerts
            .Where(d => d.ServiceName is not null)
            .GroupBy(d => d.ServiceName)
            .Any(g => g.Count() > 1))
        {
            errors.Add("Service names in alerts must be unique");
        }
        
        if (request.Dashboards
            .Where(d => d.ServiceName is not null)
            .GroupBy(d => d.ServiceName)
            .Any(g => g.Count() > 1))
        {
            errors.Add("Service names in dashboards must be unique");
        }

        if (request.Dashboards
            .Where(d => d.DashboardUid is not null)
            .GroupBy(d => d.DashboardUid)
            .Any(g => g.Count() > 1))
        {
            errors.Add("Dashboard uids must be unique");
        }

        var duplicateDashboardUids = request.Dashboards
            .Where(d => d.DashboardUid is not null)
            .GroupBy(d => d.DashboardUid)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key);

        foreach (var uid in duplicateDashboardUids)
        {
            errors.Add($"Dashboard uid '{uid}' cannot be used more than once");
        }
        
        foreach (var dashboard in request.Dashboards)
        {
            if (dashboard.DashboardUid is not null && dashboard.DashboardName is not null)
            {
                errors.Add("Can not have a dashboard uid and dashboard name");
                continue;
            }

            if (dashboard.DashboardUid is null && dashboard.ServiceName is null)
            {
                errors.Add("If dashboard uid is not provided then service name is required");
                continue;
            }

            if (dashboard.ServiceName is not null)
            {
                errors.AddRange(await ServiceNameValidator.Validate(dashboard.ServiceName, ers, cancellationToken));
            }


            if (dashboard.PromotionEnvironment != null)
            {
                errors.AddRange(PromotionEnvironmentValidator.Validate(dashboard.PromotionEnvironment));
            }
            
        }
        
        foreach (var alert in request.Alerts)
        {
            if (alert.ServiceName is null)
            {
                errors.Add("Service name must be provided");
                continue;

            }
            errors.AddRange(await ServiceNameValidator.Validate(alert.ServiceName, ers, cancellationToken));
        }
        
        return errors;
    }
}