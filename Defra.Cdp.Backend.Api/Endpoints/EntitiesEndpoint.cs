using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Microsoft.AspNetCore.Mvc;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class EntitiesEndpoint
{
    public static void MapEntitiesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/entities", CreateEntity);
        app.MapGet("/entities", GetEntities);
        app.MapGet("/entities/filters", GetFilters);
        app.MapGet("/entities/{repositoryName}", GetEntity);
        app.MapGet("/entities/{repositoryName}/status", GetEntityStatus);
        app.MapPost("/entities/{repositoryName}/tags", TagEntity);
        app.MapDelete("/entities/{repositoryName}/tags", UntagEntity);
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
    
    private static async Task<IResult> TagEntity(IEntitiesService entitiesService, string repositoryName, string tag, CancellationToken cancellationToken)
    {
        await entitiesService.AddTag(repositoryName, tag, cancellationToken);
        return Results.Ok();
    }
    
    private static async Task<IResult> UntagEntity(IEntitiesService entitiesService, string repositoryName, string tag, CancellationToken cancellationToken)
    {
        await entitiesService.RemoveTag(repositoryName, tag, cancellationToken);
        return Results.Ok();
    }
}