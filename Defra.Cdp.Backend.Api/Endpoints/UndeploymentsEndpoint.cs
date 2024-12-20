using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class UndeploymentsEndpoint
{
    public static void MapUndeploymentsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/undeployments", RegisterUndeployment);
    }

    private static async Task<IResult> RegisterUndeployment(
        IUndeploymentsService undeploymentsService,
        IValidator<RequestedUndeployment> validator,
        RequestedUndeployment requestedUndeployment,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validatedResult = await validator.ValidateAsync(requestedUndeployment, cancellationToken);
        if (!validatedResult.IsValid) return Results.ValidationProblem(validatedResult.ToDictionary());

        var logger = loggerFactory.CreateLogger("RegisterUndeployment");
        logger.LogInformation("Registering undeployment {UndeploymentId}", requestedUndeployment.UndeploymentId);

        var undeployment = Undeployment.FromRequest(requestedUndeployment);

        await undeploymentsService.RegisterUndeployment(undeployment, cancellationToken);

        return Results.Ok();
    }

}
