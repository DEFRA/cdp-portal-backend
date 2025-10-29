using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Utils.Clients;
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
        app.MapPost("/entities/{repositoryName}/decommission", StartDecommissioning);
        app.MapPost("/entities/{repositoryName}/tags", TagEntity);
        app.MapDelete("/entities/{repositoryName}/tags", UntagEntity);
    }

    private static async Task<IResult> StartDecommissioning(IEntitiesService entitiesService,
        SelfServiceOpsClient selfServiceOpsClient,
        string repositoryName,
        [FromQuery(Name = "id")] string userId,
        [FromQuery(Name = "displayName")] string userDisplayName,
        CancellationToken cancellationToken)
    {
        await entitiesService.SetDecommissionDetail(repositoryName, userId, userDisplayName, cancellationToken);
        await entitiesService.UpdateStatus(Status.Decommissioning, repositoryName, cancellationToken);
        await selfServiceOpsClient.ScaleEcsToZero(repositoryName,
            new UserDetails { Id = userId, DisplayName = userDisplayName }, cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> GetEntityStatus(IEntityStatusService entityStatusService, string repositoryName,
        CancellationToken cancellationToken)
    {
        var status = await entityStatusService.GetEntityStatus(repositoryName, cancellationToken);
        return status != null ? Results.Ok(status) : Results.NotFound();
    }

    private static async Task<IResult> GetEntities(
        [FromQuery(Name = "teamIds")] string[] teamIds,
        [FromQuery(Name = "type")] Type[] types,
        [FromQuery(Name = "status")] Status[] statuses,
        [FromQuery] string? name,
        IEntitiesService entitiesService,
        CancellationToken cancellationToken,
        [FromQuery] bool summary = true
    )
    {
        var matcher = new EntityMatcher { Types = types, Statuses = statuses, PartialName = name, TeamIds = teamIds };
        var options = new EntitySearchOptions { Summary = summary };
        return Results.Ok(await entitiesService.GetEntities(matcher, options, cancellationToken));
    }

    private static async Task<IResult> GetFilters([FromQuery(Name = "type")] Type[] types,
        [FromQuery(Name = "status")] Status[] statuses,
        IEntitiesService entitiesService, CancellationToken cancellationToken)
    {
        var filters = await entitiesService.GetFilters(types, statuses, cancellationToken);
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

    private static async Task<IResult> TagEntity(IEntitiesService entitiesService, string repositoryName, string tag,
        CancellationToken cancellationToken)
    {
        await entitiesService.AddTag(repositoryName, tag, cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> UntagEntity(IEntitiesService entitiesService, string repositoryName, string tag,
        CancellationToken cancellationToken)
    {
        await entitiesService.RemoveTag(repositoryName, tag, cancellationToken);
        return Results.Ok();
    }
}