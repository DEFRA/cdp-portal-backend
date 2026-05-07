using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Create;
using Defra.Cdp.Backend.Api.Services.Create.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class CreateResourceEndpoint
{
    public static void MapCreateResourceEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/create/s3", CreateResource);
    }

    private static async Task<Results<BadRequest<ApiError>, Ok>> CreateResource(
        [FromBody] S3BucketRequest request,
        [FromServices] ICreateResourceService createResourceService,
        [FromServices] IEntitiesService entitiesService,
        CancellationToken cancellationToken)
    {
        var entity = await entitiesService.GetEntity(request.Service, cancellationToken);
        if (entity == null)
        {
            return TypedResults.BadRequest(new ApiError($"Entity {request.Service} does not exist"));
        }

        var inputs = request.ToWorkflowInputs();
        await createResourceService.TriggerWorkflow(inputs, cancellationToken);
        return TypedResults.Ok();
    }
}