using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.GithubEvents.Model;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Bson;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class EntitiesEndpoint
{
    public static void MapEntitiesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/legacy-statuses", CreateLegacyStatus);
        app.MapPost("/legacy-statuses/update", UpdateLegacyStatus);
        app.MapPost("/legacy-statuses/{repositoryName}/overall-status", UpdateOverallStatus);
        app.MapGet("/legacy-statuses/{repositoryName}", GetLegacyStatus);
        app.MapGet("/legacy-statuses/in-progress", GetInProgressStatuses);
        app.MapGet("/legacy-statuses/in-progress/Filters", GetInProgressFilters);
    }

    private static async Task<IResult> GetInProgressStatuses([FromQuery(Name = "service")] string? service,
        [FromQuery(Name = "teamId")] string? teamId, [FromQuery(Name = "kind")] string? kind,
        ILegacyStatusService legacyStatusService, CancellationToken cancellationToken)
    {
        var statuses = await legacyStatusService.GetInProgress(service, teamId, kind, cancellationToken);
        return Results.Ok(statuses);
    }

    private static async Task<IResult> GetInProgressFilters([FromQuery(Name = "kind")] string? kind,
        ILegacyStatusService legacyStatusService, CancellationToken cancellationToken)
    {
        var statuses = await legacyStatusService.GetInProgressFilters(kind, cancellationToken);
        return statuses.Count > 0 ? Results.Ok(statuses[0]) : Results.Ok(new LegacyStatusService.InProgressFilters());
    }

    private static async Task<IResult> GetLegacyStatus(ILegacyStatusService legacyStatusService, string repositoryName,
        CancellationToken cancellationToken)
    {
        var repositoryStatus = await legacyStatusService.StatusForRepositoryName(repositoryName, cancellationToken);
        return repositoryStatus != null ? Results.Ok(repositoryStatus) : Results.NotFound();
    }

    private static async Task<IResult> CreateLegacyStatus(ILegacyStatusService legacyStatusService, LegacyStatus status,
        CancellationToken cancellationToken)
    {
        await legacyStatusService.Create(status, cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> UpdateLegacyStatus(ILegacyStatusService legacyStatusService,
        LegacyStatusUpdateRequest updateRequest,
        CancellationToken cancellationToken)
    {
        await legacyStatusService.UpdateField(updateRequest, cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> UpdateOverallStatus(ILegacyStatusService legacyStatusService,
        string repositoryName,
        CancellationToken cancellationToken)
    {
        await legacyStatusService.UpdateOverallStatus(repositoryName, cancellationToken);
        return Results.Ok();
    }
}