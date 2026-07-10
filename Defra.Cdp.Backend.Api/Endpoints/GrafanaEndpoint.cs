using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Github.Workflows;
using Defra.Cdp.Backend.Api.Services.Grafana;
using Defra.Cdp.Backend.Api.Services.Grafana.Models;
using Defra.Cdp.Backend.Api.Services.Grafana.Validators;
using Defra.Cdp.Backend.Api.Utils.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class GrafanaEndpoint
{
    private const string Repo = "cdp-grafana-modules";
    private const string DashboardPromotionWorkflow = "promote-custom-dashboard.yml";
    private const string AlertPromotionWorkflow = "promote-advanaced-alerts.yml";
    
    public static void MapGrafanaEndpoint(this IEndpointRouteBuilder app)
    {
        // temporarily turn off auth for testing
        app.MapPost("/grafana/promotions", GrafanaPromotionRequest); //.RequireAuthorization(AuthPolicies.IsTenant);
    }
    
    private static async Task<Results<BadRequest<ApiError>, Ok>> GrafanaPromotionRequest(
        [FromBody] GrafanaPromotionRequest request,
        [FromServices] IGrafanaPromotionValidator validator,
        [FromServices] ITriggerWorkflowService triggerWorkflowService,        
        [FromServices] IGrafanaPromotionRequestService requestService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var errors = await validator.Validate(request, cancellationToken);
        if (errors.Count > 0)
        {
            return TypedResults.BadRequest(new ApiError("Invalid request", errors));
        }
        var user = UserDetailsExtractor.UserDetailsFrom(httpContext.User);

        foreach (var dashboard in request.Dashboards)
        {
            var response = await triggerWorkflowService.TriggerWorkflow(Repo, DashboardPromotionWorkflow, dashboard,
                cancellationToken);
            await requestService.RecordRequest(user, dashboard, response, cancellationToken);
        }
        
        foreach (var alert in request.Alerts)
        {
            var response = await triggerWorkflowService.TriggerWorkflow(Repo, AlertPromotionWorkflow, alert,
                cancellationToken);
            await requestService.RecordRequest(user, alert, response, cancellationToken);
        }
        
        return TypedResults.Ok();
    }
    
}