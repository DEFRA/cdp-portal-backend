using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Models.Schedules;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Scheduler;
using Defra.Cdp.Backend.Api.Services.Scheduler.Mapping;
using Defra.Cdp.Backend.Api.Services.Scheduler.Model;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

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
    private static async Task<Results<BadRequest<IEnumerable<string?>>, UnauthorizedHttpResult, NotFound<string>, Conflict<string>, Created<MongoSchedule>>> CreateSchedule(
        [FromServices] ISchedulerService schedulerService,
        [FromServices] IEntitiesService entitiesService,
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
            return TypedResults.BadRequest(results.Select(r => r.ErrorMessage));
        }

        var user = ExtractedUserDetails(bearerToken);

        if (user == null)
        {
            return TypedResults.Unauthorized();
        }

        if (scheduleRequest.Task is TestSuiteTask testSuiteTask)
        {
            var entity = await entitiesService.GetEntity(testSuiteTask.EntityId, ct);

            if (entity == null)
            {
                return TypedResults.NotFound("Entity not found");
            }

            if (entity.Type != Type.TestSuite)
            {
                return TypedResults.Conflict("Entity is not a test suite");
            }
        }

        var mongoSchedule = ScheduleMapper.ToMongo(scheduleRequest, user);

        await schedulerService.Schedule(mongoSchedule, ct);
        var createdSchedule = (await schedulerService.FetchSchedules(
            new ScheduleMatchers { Id = mongoSchedule.Id },
            ct)).FirstOrDefault();

        return TypedResults.Created($"/schedules/{mongoSchedule.Id}", createdSchedule);
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
    private static async Task<Ok<List<MongoSchedule>>> GetSchedules([FromServices] ISchedulerService schedulerService,
        [FromQuery(Name = "entityId")] string? entityId,
        [FromQuery(Name = "from")] DateTime? from,
        [FromQuery(Name = "to")] DateTime? to,
        [FromQuery(Name = "enabled")] bool? enabled,
        CancellationToken ct)
    {
        var schedules = await schedulerService.FetchSchedules(
            new ScheduleMatchers() { EntityId = entityId, From = from, To = to, Enabled = enabled },
            ct);
        return TypedResults.Ok(schedules);
    }

    // GET /schedule/1234
    private static async Task<Results<NotFound, Ok<MongoSchedule>>> GetSchedule(
        [FromServices] ISchedulerService schedulerService,
        string scheduleId,
        CancellationToken ct)
    {
        var schedule = (await schedulerService.FetchSchedules(
            new ScheduleMatchers { Id = scheduleId },
            ct)).FirstOrDefault();

        return schedule is not null ? TypedResults.Ok(schedule) : TypedResults.NotFound();
    }

    // DELETE /schedules/{id}
    private static async Task<NoContent> DeleteSchedule(
        [FromServices] ISchedulerService schedulerService,
        string scheduleId,
        CancellationToken ct)
    {
        await schedulerService.DeleteSchedule(scheduleId, ct);
        return TypedResults.NoContent();
    }
}