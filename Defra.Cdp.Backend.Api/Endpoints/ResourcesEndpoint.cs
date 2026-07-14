using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Github.Workflows;
using Defra.Cdp.Backend.Api.Services.Users;
using Defra.Cdp.Backend.Api.Utils.Auth;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using static Defra.Cdp.Backend.Api.Utils.Auth.UserDetailsExtractor;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ResourcesEndpoint
{
    public static void MapResourcesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/resources/requests", CreateResourceRequest).RequireAuthorization(AuthPolicies.IsTenant);
        app.MapGet("/resources/requests", FindResourceRequests);
        app.MapGet("/resources/requests/{workflowRunId}", GetResourceRequest);
    }

    private static async Task<Results<BadRequest<ApiError>, Ok<ResourceRequestResponse>>> CreateResourceRequest(
        [FromBody] CreateTenantResourceRequest request,
        [FromServices] ICreateResourceWorkflowService createResourceWorkflowService,
        [FromServices] ICreateResourceValidator validator,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        var errors = await validator.Validate(request, cancellationToken);
        if (errors.Count > 0)
        {
            return TypedResults.BadRequest(new ApiError("Invalid request", errors));
        }
        var user = UserDetailsFrom(httpContext.User);

        var resourceRequest = await createResourceWorkflowService.CreateResources(request, user, cancellationToken);
        
        return TypedResults.Ok(ResourceRequestResponse.FromRequest(resourceRequest));
    }

    private static async Task<Results<NotFound, Ok<ResourceRequestResponse>>> GetResourceRequest(
        [FromServices] IResourceRequestService resourceRequestService,
        long workflowRunId,
        CancellationToken ct)
    {
        var resourceRequest = await resourceRequestService.FindByWorkflowId(workflowRunId, ct);

        return resourceRequest is not null
            ? TypedResults.Ok(ResourceRequestResponse.FromRequest(resourceRequest)) : TypedResults.NotFound();
    }

    private static async Task<Results<BadRequest<ApiError>, Ok<IEnumerable<ResourceRequestResponse>>>> FindResourceRequests(
        [FromServices] IResourceRequestService resourceRequestService,
        [FromServices] IEntitiesService entitiesService,
        [FromServices] IUsersService usersService,
        [AsParameters] ResourceRequestMatcher matcher,
        HttpContext httpContext,
        CancellationToken ct)
    {
        var matches = await resourceRequestService.Find(matcher, ct);
        return TypedResults.Ok(matches.Select(ResourceRequestResponse.FromRequest));
    }
}