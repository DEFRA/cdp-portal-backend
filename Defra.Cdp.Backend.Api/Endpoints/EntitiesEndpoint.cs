using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Microsoft.AspNetCore.Mvc;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class EntitiesEndpoint
{
    public static void MapEntitiesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/entities", GetEntities);
        app.MapGet("/entities/filters", GetFilters);
        app.MapPost("/entities", CreateEntity);
        app.MapPost("/entities/update", UpdateEntity);
        app.MapPost("/entities/{repositoryName}/overall-status", UpdateOverallStatus);
        app.MapGet("/entities/{repositoryName}", GetEntity);
        app.MapGet("/entities/{repositoryName}/status", GetEntityStatus);
    }

    private static async Task<IResult> GetEntityStatus(IEntityStatusService entityStatusService, string repositoryName,
        CancellationToken cancellationToken)
    {
        var status = await entityStatusService.GetEntityStatus(repositoryName, cancellationToken);
        return status != null ? Results.Ok(status) : Results.NotFound();
    }

    private static async Task<IResult> GetEntities([FromQuery] Type? type,
        [FromQuery] string? name,
        [FromQuery] string? teamId,
        IEntitiesService entitiesService, CancellationToken cancellationToken,
        [FromQuery] bool includeDecommissioned = false)
    {
        var statuses = await entitiesService.GetEntities(type, name, teamId, includeDecommissioned, cancellationToken);
        return Results.Ok(statuses);
    }

    private static async Task<IResult> GetFilters([FromQuery(Name = "type")] Type type,
        IEntitiesService entitiesService, CancellationToken cancellationToken)
    {
        var filters = await entitiesService.GetFilters(type, cancellationToken);
        return Results.Ok(filters);
    }

    private static async Task<IResult> GetEntity(IEntitiesService entitiesService, string repositoryName,
        CancellationToken cancellationToken)
    {
        var entity = await entitiesService.GetEntity(repositoryName, cancellationToken);
        return entity != null ? Results.Ok(entity) : Results.NotFound();
    }

    private static async Task<IResult> CreateEntity(IEntitiesService entitiesService, Entity entity,
        CancellationToken cancellationToken)
    {
        await entitiesService.Create(entity, cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> UpdateEntity(ILegacyStatusService legacyStatusService,
        LegacyStatusUpdateRequest updateRequest,
        CancellationToken cancellationToken)
    {
        await legacyStatusService.UpdateField(updateRequest, cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> UpdateOverallStatus(IStatusUpdateService statusUpdateService,
        string repositoryName,
        CancellationToken cancellationToken)
    {
        await statusUpdateService.UpdateOverallStatus(repositoryName, cancellationToken);
        return Results.Ok();
    }
}