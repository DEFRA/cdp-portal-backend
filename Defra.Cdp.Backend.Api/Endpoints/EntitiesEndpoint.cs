using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using Defra.Cdp.Backend.Api.Models;
using Defra.Cdp.Backend.Api.Models.Schedules;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Entities.Model;
using Defra.Cdp.Backend.Api.Services.Grafana;
using Defra.Cdp.Backend.Api.Services.MonoLambda.Handlers;
using Defra.Cdp.Backend.Api.Services.Scheduler;
using Defra.Cdp.Backend.Api.Services.Scheduler.Mapping;
using Defra.Cdp.Backend.Api.Services.Scheduler.Model;
using Defra.Cdp.Backend.Api.Utils.Clients;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Type = Defra.Cdp.Backend.Api.Services.Entities.Model.Type;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class EntitiesEndpoint
{
    private const double GrafanaPlaygroundRefreshThresholdSecs = 30; // How long we cache the playground response for
    private const long GrafanaPlaygroundWaitThresholdMs = 1900; // Just below the slow response alert threshold
    
    public static void MapEntitiesEndpoint(this IEndpointRouteBuilder app)
    {
        app.MapPost("/entities", CreateEntity);
        app.MapGet("/entities", GetEntities);
        app.MapGet("/entities/filters", GetFilters);
        app.MapGet("/entities/{name}", GetEntity);
        app.MapPost("/entities/{name}/decommission", StartDecommissioning);
        app.MapPost("/entities/{name}/tags", TagEntity);
        app.MapDelete("/entities/{name}/tags", UntagEntity);
        app.MapPost("/entities/{name}/schedules", CreateSchedule);
        app.MapGet("/entities/{name}/schedules", GetSchedules);
        app.MapGet("/entities/{name}/schedules/{scheduleId}", GetSchedule);
        app.MapPatch("/entities/{name}/schedules/{scheduleId}", UpdateSchedule);
        app.MapDelete("/entities/{name}/schedules/{scheduleId}", DeleteSchedule);
        app.MapGet("/entities/{name}/resources", GetEntityResources);
        app.MapGet("/entities/{name}/resources/{environment}", GetEntityResourcesForEnv);
        app.MapGet("/entities/{name}/topology/{environment}", GetEntityTopologyForEnv);
        app.MapGet("/entities/{name}/diagnostics/playground", GetEntityPlaygroundDashboardsAndAlerts);
    }
    
    private static async Task<Ok> StartDecommissioning(IEntitiesService entitiesService,
        ISelfServiceOpsClient selfServiceOpsClient,
        string name,
        [FromQuery(Name = "id")] string userId,
        [FromQuery(Name = "displayName")] string userDisplayName,
        CancellationToken ct)
    {
        await entitiesService.SetDecommissionDetail(name, userId, userDisplayName, ct);
        await selfServiceOpsClient.ScaleEcsToZero(name,
            new UserDetails { Id = userId, DisplayName = userDisplayName }, ct);
        return TypedResults.Ok();
    }

    [EndpointDescription("Gets a list of entities based on the provided filter." +
                         "By default the full environment details are excluded unless the summary flag is set.")]
    private static async Task<Ok<List<Entity>>> GetEntities(
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
        return TypedResults.Ok(await entitiesService.GetEntities(matcher, options, ct));
    }

    [EndpointDescription("Gets a list of values entities can be filtered on.")]
    private static async Task<Ok<EntitiesService.EntityFilters>> GetFilters(
        [FromQuery(Name = "teamIds")] string[] teamIds,
        [FromQuery(Name = "type")] Type[] types,
        [FromQuery(Name = "status")] Status[] statuses,
        IEntitiesService entitiesService, CancellationToken ct)
    {
        var filters = await entitiesService.GetFilters(teamIds, types, statuses, ct);
        return TypedResults.Ok(filters);
    }

    [EndpointDescription("Gets a single entity by name.")]
    private static async Task<Results<Ok<Entity>, NotFound>> GetEntity(IEntitiesService entitiesService, string name,
        CancellationToken ct)
    {
        var entity = await entitiesService.GetEntity(name, ct);
        return entity != null ? TypedResults.Ok(entity) : TypedResults.NotFound();
    }

    [EndpointDescription("Creates a new entity. This does trigger the actual creation of an entity, only that it was requested.")]
    private static async Task<Ok> CreateEntity(IEntitiesService entitiesService, Entity entity,
        CancellationToken ct)
    {
        await entitiesService.Create(entity, ct);
        return TypedResults.Ok();
    }

    [EndpointDescription("Adds a 'tag' (e.g. PRR, BETA) to an entity.")]
    private static async Task<Ok> TagEntity(IEntitiesService entitiesService, string name, string tag,
        CancellationToken ct)
    {
        await entitiesService.AddTag(name, tag, ct);
        return TypedResults.Ok();
    }

    [EndpointDescription("Removes a 'tag' (e.g. PRR, BETA) to an entity.")]
    private static async Task<Ok> UntagEntity(IEntitiesService entitiesService, string name, string tag,
        CancellationToken ct)
    {
        await entitiesService.RemoveTag(name, tag, ct);
        return TypedResults.Ok();
    }

    
    private static async Task<Results<BadRequest<List<string?>>, UnauthorizedHttpResult, NotFound<string>, Conflict<string>, Created<MongoSchedule>>> CreateSchedule(
        [FromServices] IEntitiesService entitiesService,
        [FromServices] ISchedulerService schedulerService,
        [FromRoute] string name,
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
            return TypedResults.BadRequest(results.Select(r => r.ErrorMessage).ToList());
        }

        var user = ExtractedUserDetails(bearerToken);

        if (user == null)
        {
            return TypedResults.Unauthorized();
        }

        var entity = await entitiesService.GetEntity(name, ct);

        if (entity == null)
        {
            return TypedResults.NotFound("Entity not found");
        }

        if (scheduleRequest.Task is EntityTestSuiteTask && entity.Type != Type.TestSuite)
        {
            return TypedResults.Conflict("Entity is not a test suite");
        }

        if (scheduleRequest.Task is EntityDeployServiceTask && entity.Type != Type.Microservice)
        {
            return TypedResults.Conflict("Entity is not a microservice");
        }

        var mongoSchedule = ScheduleMapper.ToMongo(scheduleRequest, user, name);
        await schedulerService.Schedule(mongoSchedule, ct);


        var createdSchedule = (await schedulerService.FetchSchedules(
            new ScheduleMatchers { Id = mongoSchedule.Id },
            ct)).FirstOrDefault();

        return TypedResults.Created($"/entities/{name}/schedules/{mongoSchedule.Id}", createdSchedule);
    }
    
    private static async Task<Results<BadRequest<List<string?>>, UnauthorizedHttpResult, NotFound<string>, Conflict<string>, Ok<MongoSchedule>>> UpdateSchedule(
        [FromServices] IEntitiesService entitiesService,
        [FromServices] ISchedulerService schedulerService,
        [FromRoute] string name,
        [FromRoute] string scheduleId,
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
            return TypedResults.BadRequest(results.Select(r => r.ErrorMessage).ToList());
        }

        var user = ExtractedUserDetails(bearerToken);

        if (user == null)
        {
            return TypedResults.Unauthorized();
        }

        var entity = await entitiesService.GetEntity(name, ct);

        if (entity == null)
        {
            return TypedResults.NotFound("Entity not found");
        }
        
        var originalSchedule = (await schedulerService.FetchSchedules(
            new ScheduleMatchers { Id = scheduleId },
            ct)).FirstOrDefault();

        if (originalSchedule == null)
        {
            return TypedResults.NotFound("Schedule id not found");
        }

        if (scheduleRequest.Task is EntityTestSuiteTask && entity.Type != Type.TestSuite)
        {
            return TypedResults.Conflict("Entity is not a test suite");
        }

        if (scheduleRequest.Task is EntityDeployServiceTask && entity.Type != Type.Microservice)
        {
            return TypedResults.Conflict("Entity is not a microservice");
        }
        
        var mongoSchedule = ScheduleMapper.ToUpdatedMongo(scheduleRequest, originalSchedule, user, name);
        await schedulerService.UpdateAsync(scheduleId, mongoSchedule, ct);

        var updatedSchedule = (await schedulerService.FetchSchedules(
            new ScheduleMatchers { Id = scheduleId },
            ct)).FirstOrDefault();
        
        return TypedResults.Ok(updatedSchedule);
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

    private static async Task<Results<NotFound, Ok<List<MongoSchedule>>>> GetSchedules(
        [FromServices] IEntitiesService entitiesService,
        [FromServices] ISchedulerService schedulerService,
        string name,
        CancellationToken ct)
    {
        var entity = await entitiesService.GetEntity(name, ct);

        if (entity == null)
        {
            return TypedResults.NotFound();
        }

        var schedules = await schedulerService.FetchSchedules(
            new ScheduleMatchers() { EntityId = name },
            ct);
        return TypedResults.Ok(schedules);
    }

    private static async Task<Results<NotFound, Ok<MongoSchedule>>> GetSchedule(
        [FromServices] IEntitiesService entitiesService,
        [FromServices] ISchedulerService schedulerService,
        string name,
        string scheduleId,
        CancellationToken ct)
    {
        var entity = await entitiesService.GetEntity(name, ct);

        if (entity == null)
        {
            return TypedResults.NotFound();
        }

        var schedule = (await schedulerService.FetchSchedules(
            new ScheduleMatchers { Id = scheduleId },
            ct)).FirstOrDefault();

        return schedule is not null ? TypedResults.Ok(schedule) : TypedResults.NotFound();
    }

    private static async Task<NoContent> DeleteSchedule(
        [FromServices] ISchedulerService schedulerService,
        string scheduleId,
        CancellationToken ct)
    {
        await schedulerService.DeleteSchedule(scheduleId, ct);
        return TypedResults.NoContent();
    }
    
    private static async Task<Results<NotFound, Ok<Dictionary<string, EntityResources>>>> GetEntityResources(
        [FromServices] IEntitiesService entitiesService,
        string name,
        CancellationToken ct)
    {
        var entity = await entitiesService.GetEntity(name, ct);
        if (entity == null) return TypedResults.NotFound();

        var environments = new Dictionary<string, EntityResources>();

        foreach (var env in entity.Environments.Keys)
        {
            environments[env] = EntityResourceMapper.FromCdpTenant(entity.Environments[env]);
        }
        return TypedResults.Ok(environments);
    }

    private static async Task<Results<NotFound, Ok<EntityResources>>> GetEntityResourcesForEnv(
        [FromServices] IEntitiesService entitiesService,
        string name,
        string environment,
        CancellationToken ct)
    {
        var entity = await entitiesService.GetEntity(name, ct);
        if (entity == null) return TypedResults.NotFound();

        var resources = entity.Environments.TryGetValue(environment, out var entityEnvironment)
            ? EntityResourceMapper.FromCdpTenant(entityEnvironment)
            : new EntityResources();
        return TypedResults.Ok(resources);
    }
    
    private static async Task<Results<NotFound, Ok<List<TopologyService>>>> GetEntityTopologyForEnv(
        [FromServices] IEntityTopologyService entityTopologyService,
        string name,
        string environment,
        CancellationToken ct)
    {
        var relationships = await entityTopologyService.ListTopologyOfEntity(name, environment, ct);
        if (relationships.Count == 0)
        {
            return TypedResults.NotFound();
        }
        return TypedResults.Ok(relationships);
    }
    
    private static async Task<Results<NotFound, Accepted<ApiError>, Ok<GrafanaPlaygroundResources>>> GetEntityPlaygroundDashboardsAndAlerts(
        [FromServices] IEntitiesService entitiesService,
        [FromServices] IGrafanaPlaygroundService grafanaPlaygroundService,
        string name,
        CancellationToken ct)
    {
        var entity = await entitiesService.GetEntity(name, ct);
        if (entity == null) return TypedResults.NotFound();
        
        var playgroundResources = await grafanaPlaygroundService.FindPlaygroundsForService(name, ct);
        if (playgroundResources == null || (DateTime.UtcNow - playgroundResources.Updated).TotalSeconds > GrafanaPlaygroundRefreshThresholdSecs )
        {
            var requestId = await grafanaPlaygroundService.RequestUpdateForService(name, ct);
            var result = await grafanaPlaygroundService.WaitForUpdate(requestId, GrafanaPlaygroundWaitThresholdMs, ct);

            if (result == null)
            {
                return TypedResults.Accepted($"/entities/{name}/diagnostics/playground",
                    new ApiError("Grafana did not response in time, try again"));
            }
            return TypedResults.Ok(result);
        }

        
        return TypedResults.Ok(playgroundResources);
    }
}