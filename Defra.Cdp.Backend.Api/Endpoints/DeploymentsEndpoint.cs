using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Tenants;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class DeploymentsEndpoint
{
    private const string DeploymentsBaseRoute = "deployments";
    private const string WhatsRunningWhereRoute = "whats-running-where";
    private const string DeploymentsTag = "Deployments";

    public static IEndpointRouteBuilder MapDeploymentsEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet(DeploymentsBaseRoute,
                async (IDeploymentsService deploymentsService,
                    CancellationToken cancellationToken,
                    [FromQuery(Name = "offset")] int? offset,
                    [FromQuery(Name = "environment")] string? environment
                ) =>
                {
                    var o = offset ?? 0;
                    return await FindLatestDeployments(deploymentsService, cancellationToken, o, environment);
                })
            .RequireAuthorization()
            .AllowAnonymous()
            .WithName("GetDeployments")
            .Produces<IEnumerable<Deployment>>()
            .WithTags(DeploymentsTag);

        app.MapGet($"{DeploymentsBaseRoute}/{{deploymentId}}", FindDeployment)
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
    private static async Task<IResult> FindLatestDeployments(IDeploymentsService deploymentsService,
        CancellationToken cancellationToken, int offset,
        string? environment)
    {
        var deployables = await deploymentsService.FindLatest(environment, offset, cancellationToken);
        return Results.Ok(deployables);
    }

    // Get /deployments/{deploymentId}
    private static async Task<IResult> FindDeployment(IDeploymentsService deploymentsService, string deploymentId,
        CancellationToken cancellationToken)
    {
        var deployment = await deploymentsService.FindDeployment(deploymentId, cancellationToken);
        return deployment == null
            ? Results.NotFound(new { Message = $"{deploymentId} not found" })
            : Results.Ok(deployment);
    }

    private static async Task<IResult> WhatsRunningWhere(IDeploymentsService deploymentsService,
        CancellationToken cancellationToken)
    {
        var deployments = await deploymentsService.FindWhatsRunningWhere(cancellationToken);
        return Results.Ok(deployments);
    }

    private static async Task<IResult> WhatsRunningWhereForService(IDeploymentsService deploymentsService,
        string service, CancellationToken cancellationToken)
    {
        var deployments = await deploymentsService.FindWhatsRunningWhere(service, cancellationToken);
        return Results.Ok(deployments);
    }

    private static async Task<IResult> RegisterDeployment(IDeploymentsService deploymentsService,
        IValidator<RequestedDeployment> validator,
        RequestedDeployment rd,
        CancellationToken cancellationToken)
    {
        var validatedResult = await validator.ValidateAsync(rd, cancellationToken);

        if (!validatedResult.IsValid) return Results.ValidationProblem(validatedResult.ToDictionary());

        var deployment = new Deployment
        {
            DeployedAt = DateTime.UtcNow,
            Environment = rd.Environment,
            Service = rd.Service,
            Status = "REQUESTED",
            UserId = rd.User?.Id,
            User = rd.User?.DisplayName,
            Version = rd.Version,
            DockerImage = rd.Service
        };

        await deploymentsService.Insert(deployment, cancellationToken);
        return Results.Ok();
    }
}