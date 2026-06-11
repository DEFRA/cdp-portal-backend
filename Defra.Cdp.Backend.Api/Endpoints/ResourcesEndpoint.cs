using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Utils.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using static Defra.Cdp.Backend.Api.Utils.Auth.UserDetailsExtractor;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ResourcesEndpoint
{
    public static void MapResourcesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/resources", CreateResource).RequireAuthorization(AuthPolicies.IsTenant);
    }
    
    private static async Task<Results<BadRequest<ApiError>, Ok<GitHubTriggerWorkflowResponse>>> CreateResource(
        [FromBody] CreateTenantResourceRequest request,
        [FromServices] ICreateResourceWorkflowService createResourceWorkflowService,
        [FromServices] IEntitiesService entitiesService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        // TODO: validate payload & user is an owner of all of the services involved.
        var user = UserDetailsFrom(httpContext.User);
        var response = await createResourceWorkflowService.CreateResources(request, user!, cancellationToken);
        return TypedResults.Ok(response.Workflow);
    }
}