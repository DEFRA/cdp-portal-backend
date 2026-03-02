using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Models.Schedules;
using Defra.Cdp.Backend.Api.Services.scheduler;
using Defra.Cdp.Backend.Api.Services.scheduler.Mapping;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class SchedulesEndpoint
{
    public static void MapSchedulesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/schedules", CreateSchedule);
        app.MapGet("/schedules", GetSchedules);
        app.MapGet("/schedules/{scheduleId}", GetSchedule);
        app.MapDelete("/schedules/{scheduleId}", DeleteSchedule);
    }

    // POST /schedules
    private static async Task<IResult> CreateSchedule([FromServices] ISchedulerService schedulerService,
        [FromBody] ScheduleRequest scheduleRequest,
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

        var mongoSchedule = ScheduleMapper.ToMongo(scheduleRequest, user);

        await schedulerService.Schedule(mongoSchedule, ct);
        var createdSchedule = (await schedulerService.FetchSchedules(
            new ScheduleMatchers { Id = mongoSchedule.Id },
            ct)).FirstOrDefault();

        return Results.Created($"/schedules/{mongoSchedule.Id}", createdSchedule);
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

    // GET /schedules or with query params GET /schedules?entityId=my-service&enabled=true
    private static async Task<IResult> GetSchedules([FromServices] ISchedulerService schedulerService,
        [FromQuery(Name = "entityId")] string? entityId,
        [FromQuery(Name = "from")] DateTime? from,
        [FromQuery(Name = "before")] DateTime? before,
        [FromQuery(Name = "enabled")] bool? enabled,
        CancellationToken ct)
    {
        var schedules = await schedulerService.FetchSchedules(
            new ScheduleMatchers() { EntityId = entityId, From = from, Before = before, Enabled = enabled },
            ct);
        return Results.Ok(schedules);
    }

    // GET /schedule/1234
    private static async Task<IResult> GetSchedule(
        [FromServices] ISchedulerService schedulerService,
        string scheduleId,
        CancellationToken ct)
    {
        var schedule = (await schedulerService.FetchSchedules(
            new ScheduleMatchers { Id = scheduleId },
            ct)).FirstOrDefault();

        return schedule is not null ? Results.Ok(schedule) : Results.NotFound();
    }

    // DELETE /schedules/{id}
    private static async Task<IResult> DeleteSchedule([FromServices] ISchedulerService schedulerService, string id,
        CancellationToken ct)
    {
        await schedulerService.DeleteSchedule(id, ct);
        return Results.NoContent();
    }
}