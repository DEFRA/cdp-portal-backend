using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Grafana;
using Defra.Cdp.Backend.Api.Services.Grafana.Models;
using Defra.Cdp.Backend.Api.Services.Grafana.Validators;
using Defra.Cdp.Backend.Api.Utils.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class GrafanaEndpoint
{
    public static void MapGrafanaEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/grafana/promotions", GrafanaPromotionRequest).RequireAuthorization(AuthPolicies.IsTenant);
    }
    
    private static async Task<Results<BadRequest<ApiError>, Ok>> GrafanaPromotionRequest(
        [FromBody] GrafanaPromotionRequest request,
        [FromServices] IGrafanaPromotionValidator validator,
        [FromServices] IGrafanaPromotionService grafanaPromotionService,        
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
            await grafanaPromotionService.PromoteDashboard(dashboard, user, cancellationToken);
        }
        
        foreach (var alert in request.Alerts)
        {
            await grafanaPromotionService.PromoteAlerts(alert, user, cancellationToken);
        }
        
        return TypedResults.Ok();
    }
    
}