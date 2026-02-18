using System.ComponentModel.DataAnnotations;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Services.scheduler;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class ScheduleEndpoint
{
    public static void MapSchedulingEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapGet("/schedules", GetSchedules);
        app.MapPost("/schedules", CreateSchedule);
        app.MapDelete("/schedules/{id}", DeleteSchedule);
    }

    // POST /schedules
    private static async Task<IResult> CreateSchedule(ISchedulerService schedulerService,
        [FromBody] ScheduleRequest scheduleRequest, CancellationToken ct)
    {
        var context = new ValidationContext(scheduleRequest.Config);
        var results = new List<ValidationResult>();
        var isValid =
            Validator.TryValidateObject(scheduleRequest.Config, context, results, validateAllProperties: true);

        // todo get this from auth token
        UserDetails user = new() { Id = "00000000-0000-0000-0000-00000000002", DisplayName = "test user" };

        if (!isValid)
        {
            return Results.BadRequest(results.Select(r => r.ErrorMessage));
        }

        await schedulerService.Schedule(scheduleRequest, user, ct);
        return Results.Ok();
    }

    // GET /schedules or with query params GET /schedules?id=1234&team=cdp
    private static async Task<IResult> GetSchedules(ISchedulerService schedulerService,
        [FromQuery(Name = "id")] string? id,
        [FromQuery(Name = "team")] string? teamId,
        [FromQuery(Name = "from")] DateTime? from,
        [FromQuery(Name = "before")] DateTime? before,
        [FromQuery(Name = "enabled")] bool? enabled,
        CancellationToken ct)
    {
        var schedules = await schedulerService.FetchSchedules(
            new ScheduleMatchers()
            {
                Id = id,
                TeamId = teamId,
                From = from,
                Before = before,
                Enabled = enabled
            },
            ct);
        return Results.Ok(schedules);
    }

    // DELETE /schedules/{id}
    private static async Task<IResult> DeleteSchedule(ISchedulerService schedulerService, string id,
        CancellationToken ct)
    {
        await schedulerService.DeleteSchedule(id, ct);
        return Results.Ok();
    }
}