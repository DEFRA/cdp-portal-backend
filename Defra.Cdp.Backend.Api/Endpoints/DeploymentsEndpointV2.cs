using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Deployments;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class DeploymentsEndpointV2
{
    public static IEndpointRouteBuilder MapDeploymentsEndpointV2(this IEndpointRouteBuilder app)
    {
        app.MapGet("/v2/deployments",
            async (IDeploymentsServiceV2 deploymentsService,
                CancellationToken cancellationToken,
                [FromQuery(Name = "offset")] int? offset,
                [FromQuery(Name = "environment")] string? environment,
                [FromQuery(Name = "page")] int? page,
                [FromQuery(Name = "size")] int? size
            ) =>
            {
                var o = offset ?? 0;
                var p = page ?? DeploymentsServiceV2.DefaultPage;
                var s = size ?? DeploymentsServiceV2.DefaultPageSize;
                return await FindLatestDeployments(deploymentsService, environment, o, p, s, cancellationToken);
            });

        app.MapGet("/v2/deployments/{deploymentId}", FindDeployments);
        app.MapGet("/v2/whats-running-where", WhatsRunningWhere);
        app.MapGet("/v2/whats-running-where/{service}", WhatsRunningWhereForService);
        app.MapPost("/v2/deployments", RegisterDeployment);
        app.MapGet("/v2/deployment-config/{service}/{environment}", DeploymentConfig);

        return app;
    }

    // GET /deployments or GET /deployments?offet=23
    internal static async Task<IResult> FindLatestDeployments(IDeploymentsServiceV2 deploymentsService,
        string? environment,
        int offset,
        int page,
        int size,
        CancellationToken cancellationToken)
    {
        var deploymentsPage = await deploymentsService.FindLatest(environment, offset, page, size, cancellationToken);
        return Results.Ok(deploymentsPage);
    }

    // Get /deployments/{deploymentId}
    internal static async Task<IResult> FindDeployments(IDeploymentsServiceV2 deploymentsService, string deploymentId,
        CancellationToken cancellationToken)
    {
        var deployment = await deploymentsService.FindDeployment(deploymentId, cancellationToken);

        return deployment == null
            ? Results.NotFound(new { Message = $"{deploymentId} was not found" })
            : Results.Ok(deployment);
    }

    internal static async Task<IResult> WhatsRunningWhere(IDeploymentsServiceV2 deploymentsService,
        HttpContext httpContext,
        CancellationToken cancellationToken)
    {
        List<string> environments = httpContext.Request.Query["environments"].Where(g => g != null).ToList()!;
        var deployments = await deploymentsService.FindWhatsRunningWhere(environments, cancellationToken);
        return Results.Ok(deployments);
    }

    internal static async Task<IResult> WhatsRunningWhereForService(IDeploymentsServiceV2 deploymentsService,
        string service, CancellationToken cancellationToken)
    {
        var deployments = await deploymentsService.FindWhatsRunningWhere(service, cancellationToken);
        return Results.Ok(deployments);
    }

    internal static async Task<IResult> RegisterDeployment(
        IDeploymentsServiceV2 deploymentsServiceV2,
        IValidator<RequestedDeployment> validator,
        RequestedDeployment rd,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validatedResult = await validator.ValidateAsync(rd, cancellationToken);
        if (!validatedResult.IsValid) return Results.ValidationProblem(validatedResult.ToDictionary());

        var logger = loggerFactory.CreateLogger("RegisterDeployment");
        logger.LogInformation("Registering deployment {RdDeploymentId}", rd.DeploymentId);
        await deploymentsServiceV2.RegisterDeployment(rd, cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> DeploymentConfig(
        IDeploymentsServiceV2 deploymentsService,
        string service,
        string environment,
        CancellationToken cancellationToken)
    {
        var result = await deploymentsService.FindDeploymentConfig(service, environment, cancellationToken);
        return result == null ? Results.NotFound(new ApiError("Not found")) : Results.Ok(result);
    }
}