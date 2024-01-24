using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Tenants;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class DeploymentsEndpoint
{
    private const string DeploymentsBaseRoute = "deployments";
    private const string SquashedDeploymentsBaseRoute = "squashed-deployments";
    private const string WhatsRunningWhereRoute = "whats-running-where";
    private const string DeploymentsTag = "Deployments";

    public static IEndpointRouteBuilder MapDeploymentsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet(DeploymentsBaseRoute,
                async (IDeploymentsService deploymentsService,
                    CancellationToken cancellationToken,
                    [FromQuery(Name = "offset")] int? offset,
                    [FromQuery(Name = "environment")] string? environment,
                    [FromQuery(Name = "page")] int? page,
                    [FromQuery(Name = "size")] int? size
                ) =>
                {
                    var o = offset ?? 0;
                    var p = page ?? DeploymentsService.DefaultPage;
                    var s = size ?? DeploymentsService.DefaultPageSize;
                    return await FindLatestDeployments(deploymentsService, cancellationToken, o, environment, p,
                        s);
                })
            .RequireAuthorization()
            .AllowAnonymous()
            .WithName("GetDeployments")
            .Produces<DeploymentsPage>()
            .WithTags(DeploymentsTag);

        app.MapGet(SquashedDeploymentsBaseRoute,
                async (IDeploymentsService deploymentsService,
                    CancellationToken cancellationToken,
                    [FromQuery(Name = "offset")] int? offset,
                    [FromQuery(Name = "environment")] string? environment,
                    [FromQuery(Name = "page")] int? page,
                    [FromQuery(Name = "size")] int? size
                ) =>
                {
                    var o = offset ?? 0;
                    var p = page ?? DeploymentsService.DefaultPage;
                    var s = size ?? DeploymentsService.DefaultPageSize;
                    return await FindSquashedLatestDeployments(deploymentsService, cancellationToken, o, environment, p,
                        s);
                })
            .RequireAuthorization()
            .AllowAnonymous()
            .WithName("GetSquashedDeployments")
            .Produces<SquashedDeploymentsPage>()
            .WithTags(DeploymentsTag);

        app.MapGet($"{DeploymentsBaseRoute}/{{deploymentId}}", FindDeployments)
            .WithName("GetDeploymentById")
            .Produces<Deployment>()
            .Produces(StatusCodes.Status404NotFound)
            .WithTags(DeploymentsTag);

        app.MapGet(WhatsRunningWhereRoute, WhatsRunningWhere)
            .WithName("GetWhatsWhere")
            .Produces<IEnumerable<Deployment>>()
            .WithTags(DeploymentsTag);

        app.MapGet($"{WhatsRunningWhereRoute}/{{service}}", WhatsRunningWhereForService)
            .WithName("GetWhatsWhereForService")
            .Produces<IEnumerable<Deployment>>()
            .WithTags(DeploymentsTag);

        app.MapPost($"{DeploymentsBaseRoute}", RegisterDeployment)
            .WithName("PostDeployments")
            .WithTags(DeploymentsTag);

        return app;
    }

    // GET /deployments or GET /deployments?offet=23
    internal static async Task<IResult> FindLatestDeployments(IDeploymentsService deploymentsService,
        CancellationToken cancellationToken, int offset,
        string? environment, int page, int size)
    {
        var deploymentsPage = await deploymentsService.FindLatest(environment, offset, page, size, cancellationToken);
        return Results.Ok(deploymentsPage);
    }

    internal static async Task<IResult> FindSquashedLatestDeployments(IDeploymentsService deploymentsService,
        CancellationToken cancellationToken, int offset,
        string? environment, int page, int size)
    {
        var deploymentsPage =
            await deploymentsService.FindLatestSquashed(environment, page, size, cancellationToken);
        return Results.Ok(deploymentsPage);
    }

    // Get /deployments/{deploymentId}
    internal static async Task<IResult> FindDeployments(IDeploymentsService deploymentsService, string deploymentId,
        CancellationToken cancellationToken)
    {
        var deployment = await deploymentsService.FindDeployments(deploymentId, cancellationToken);
        return Results.Ok(deployment);
    }

    internal static async Task<IResult> WhatsRunningWhere(IDeploymentsService deploymentsService,
        CancellationToken cancellationToken)
    {
        var deployments = await deploymentsService.FindWhatsRunningWhere(cancellationToken);
        return Results.Ok(deployments);
    }

    internal static async Task<IResult> WhatsRunningWhereForService(IDeploymentsService deploymentsService,
        string service, CancellationToken cancellationToken)
    {
        var deployments = await deploymentsService.FindWhatsRunningWhere(service, cancellationToken);
        return Results.Ok(deployments);
    }

    internal static async Task<IResult> RegisterDeployment(IDeploymentsService deploymentsService,
        IValidator<RequestedDeployment> validator,
        RequestedDeployment rd,
        ILoggerFactory loggerFactory,
        CancellationToken cancellationToken)
    {
        var validatedResult = await validator.ValidateAsync(rd, cancellationToken);

        if (!validatedResult.IsValid) return Results.ValidationProblem(validatedResult.ToDictionary());

        var logger = loggerFactory.CreateLogger("RegisterDeployment");
        logger.LogInformation("Registering deployment {RdDeploymentId}", rd.DeploymentId);

        var deployment = new Deployment
        {
            DeployedAt = DateTime.UtcNow,
            Environment = rd.Environment,
            Service = rd.Service,
            Status = "REQUESTED",
            DesiredStatus = null,
            UserId = rd.User?.Id,
            User = rd.User?.DisplayName,
            Version = rd.Version,
            DockerImage = rd.Service,
            InstanceCount = rd.InstanceCount,
            Cpu = rd.Cpu,
            Memory = rd.Memory,
            DeploymentId = rd.DeploymentId
        };

        await deploymentsService.Insert(deployment, cancellationToken);
        return Results.Ok();
    }
}