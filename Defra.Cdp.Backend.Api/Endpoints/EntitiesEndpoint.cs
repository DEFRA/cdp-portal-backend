using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Models.Schedules;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.scheduler;
using Defra.Cdp.Backend.Api.Services.scheduler.Mapping;
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
        app.MapPost("/entities/{repositoryName}/decommission", StartDecommissioning);
        app.MapPost("/entities/{repositoryName}/tags", TagEntity);
        app.MapDelete("/entities/{repositoryName}/tags", UntagEntity);
        app.MapPost("/entities/{repositoryName}/schedules", CreateSchedule);
        app.MapGet("/entities/{repositoryName}/schedules", GetSchedules);
        app.MapGet("/entities/{repositoryName}/schedules/{scheduleId}", GetSchedule);
        app.MapDelete("/entities/{repositoryName}/schedules/{scheduleId}", DeleteSchedule);
    }

    private static async Task<IResult> StartDecommissioning(IEntitiesService entitiesService,
        ISelfServiceOpsClient selfServiceOpsClient,
        string repositoryName,
        [FromQuery(Name = "id")] string userId,
        [FromQuery(Name = "displayName")] string userDisplayName,
        CancellationToken ct)
    {
        await entitiesService.SetDecommissionDetail(repositoryName, userId, userDisplayName, ct);
        await selfServiceOpsClient.ScaleEcsToZero(repositoryName,
            new UserDetails { Id = userId, DisplayName = userDisplayName }, ct);
        return Results.Ok();
    }

    private static async Task<IResult> GetEntities(
        [FromQuery(Name = "teamIds")] string[] teamIds,
        [FromQuery(Name = "type")] Type[] types,
        [FromQuery(Name = "status")] Status[] statuses,
        [FromQuery] string? name,
        IEntitiesService entitiesService,
        CancellationToken ct,
        [FromQuery] bool summary = true
    )
    {
        var matcher = new EntityMatcher { Types = types, Statuses = statuses, PartialName = name, TeamIds = teamIds };
        var options = new EntitySearchOptions { Summary = summary };
        return Results.Ok(await entitiesService.GetEntities(matcher, options, ct));
    }

    private static async Task<IResult> GetFilters(
        [FromQuery(Name = "teamIds")] string[] teamIds,
        [FromQuery(Name = "type")] Type[] types,
        [FromQuery(Name = "status")] Status[] statuses,
        IEntitiesService entitiesService, CancellationToken ct)
    {
        var filters = await entitiesService.GetFilters(teamIds, types, statuses, ct);
        return Results.Ok(filters);
    }

    private static async Task<IResult> GetEntity(IEntitiesService entitiesService, string repositoryName,
        CancellationToken ct)
    {
        var entity = await entitiesService.GetEntity(repositoryName, ct);
        return entity != null ? Results.Ok(entity) : Results.NotFound();
    }

    private static async Task<IResult> CreateEntity(IEntitiesService entitiesService, Entity entity,
        CancellationToken ct)
    {
        await entitiesService.Create(entity, ct);
        return Results.Ok();
    }

    private static async Task<IResult> TagEntity(IEntitiesService entitiesService, string repositoryName, string tag,
        CancellationToken ct)
    {
        await entitiesService.AddTag(repositoryName, tag, ct);
        return Results.Ok();
    }

    private static async Task<IResult> UntagEntity(IEntitiesService entitiesService, string repositoryName, string tag,
        CancellationToken ct)
    {
        await entitiesService.RemoveTag(repositoryName, tag, ct);
        return Results.Ok();
    }

    private static async Task<IResult> CreateSchedule(
        [FromServices] IEntitiesService entitiesService,
        [FromServices] ISchedulerService schedulerService,
        string repositoryName,
        [FromBody] EntityScheduleRequest scheduleRequest,
        [FromHeader(Name = "Authorization")] string? bearerToken,
        CancellationToken ct)
    {
        var context = new ValidationContext(scheduleRequest.Config);
        var results = new List<ValidationResult>();
        var isValid =
            Validator.TryValidateObject(scheduleRequest.Config, context, results, validateAllProperties: true);

        if (!isValid)
        {
            return Results.BadRequest(results.Select(r => r.ErrorMessage));
        }

        var user = ExtractedUserDetails(bearerToken);

        if (user == null)
        {
            return Results.Unauthorized();
        }

        var mongoSchedule = ScheduleMapper.ToMongo(scheduleRequest, user, repositoryName);
        await schedulerService.Schedule(mongoSchedule, ct);


        var createdSchedule = (await schedulerService.FetchSchedules(
            new ScheduleMatchers { Id = mongoSchedule.Id },
            ct)).FirstOrDefault();

        return Results.Created($"/entities/{repositoryName}/schedules/{mongoSchedule.Id}", createdSchedule);
    }

    // temporary until we play auth ticket 
    private static UserDetails? ExtractedUserDetails(string? bearerToken)
    {
        if (string.IsNullOrEmpty(bearerToken) || !bearerToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = bearerToken["Bearer ".Length..].Trim();
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        var oid = jwt.Claims.FirstOrDefault(c => c.Type == "oid")?.Value;
        var name = jwt.Claims.FirstOrDefault(c => c.Type == "name")?.Value;
        if (string.IsNullOrEmpty(oid) || string.IsNullOrEmpty(name))
        {
            return null;
        }

        return new UserDetails { Id = oid, DisplayName = name };
    }

    private static async Task<IResult> GetSchedules(
        [FromServices] IEntitiesService entitiesService,
        [FromServices] ISchedulerService schedulerService,
        string repositoryName,
        CancellationToken ct)
    {
        var entity = await entitiesService.GetEntity(repositoryName, ct);

        if (entity == null)
        {
            return Results.NotFound();
        }

        var schedules = await schedulerService.FetchSchedules(
            new ScheduleMatchers() { EntityId = repositoryName },
            ct);
        return Results.Ok(schedules);
    }

    private static async Task<IResult> GetSchedule(
        [FromServices] IEntitiesService entitiesService,
        [FromServices] ISchedulerService schedulerService,
        string repositoryName,
        string scheduleId,
        CancellationToken ct)
    {
        var entity = await entitiesService.GetEntity(repositoryName, ct);

        if (entity == null)
        {
            return Results.NotFound();
        }

        var schedule = (await schedulerService.FetchSchedules(
            new ScheduleMatchers { Id = scheduleId },
            ct)).FirstOrDefault();

        return schedule is not null ? Results.Ok(schedule) : Results.NotFound();
    }

    private static async Task<IResult> DeleteSchedule(
        [FromServices] ISchedulerService schedulerService,
        string scheduleId,
        CancellationToken ct)
    {
        await schedulerService.DeleteSchedule(scheduleId, ct);
        return Results.NoContent();
    }
}