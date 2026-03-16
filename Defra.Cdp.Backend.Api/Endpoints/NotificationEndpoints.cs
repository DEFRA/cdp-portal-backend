using System.ComponentModel.DataAnnotations;
using Defra.Cdp.Backend.Api.Services.Entities;
using Defra.Cdp.Backend.Api.Services.Notifications;
using Defra.Cdp.Backend.Api.Utils;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/entities/{entityId}/notifications", CreateNotification);
        app.MapGet("/entities/{entityId}/notifications", FindNotificationRulesForEntity);
        app.MapGet("/entities/{entityId}/supported-notifications", FindSupportedNotifications);
        app.MapGet("/entities/{entityId}/notifications/{ruleId}", GetNotificationRule).WithName("GetNotificationRule");
        app.MapPut("/entities/{entityId}/notifications/{ruleId}", UpdateNotification);
        app.MapDelete("/entities/{entityId}/notifications/{ruleId}", DeleteNotification);
    }

    [EndpointDescription("Creates a new notification for an entity")]
    private static async Task<Results<BadRequest<IEnumerable<string?>>, CreatedAtRoute>> CreateNotification(
        [FromServices] INotificationRuleService notificationRuleService,
        [FromRoute] string entityId,
        [FromBody] CreateRuleRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        if (!isValid)
        {
            return TypedResults.BadRequest(results.Select(r => r.ErrorMessage));
        }

        var rule = request.ToRule(entityId);
        await notificationRuleService.SaveAsync(rule, cancellationToken);

        return TypedResults.CreatedAtRoute(
            routeName: "GetNotificationRule",
            routeValues: new { entityId = rule.Entity, ruleId = rule.RuleId }
        );
    }

    [EndpointDescription("Updates an existing notification by its rule ID")]
    private static async Task<Results<BadRequest<IEnumerable<string?>>, NotFound<string>, Ok>> UpdateNotification(
        [FromServices] INotificationRuleService notificationRuleService,
        [FromRoute] string entityId,
        [FromRoute] string ruleId,
        [FromBody] UpdateRuleRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<ValidationResult>();
        var isValid = Validator.TryValidateObject(request, new ValidationContext(request), results, validateAllProperties: true);
        if (!isValid)
        {
            return TypedResults.BadRequest(results.Select(r => r.ErrorMessage));
        }

        
        var rule = await notificationRuleService.FindRule(ruleId, cancellationToken);
        if (rule == null)
        {
            return TypedResults.NotFound($"rule {ruleId} not found");
        }
        
        await notificationRuleService.UpdateAsync(request.ToRule(entityId, rule), cancellationToken);
        return TypedResults.Ok();
    }

    [EndpointDescription("Deletes an existing notification by its rule ID")]
    private static async Task<Results<NotFound<string>, BadRequest<string>, Ok>> DeleteNotification(
        [FromServices] INotificationRuleService notificationRuleService,
        [FromRoute] string entityId,
        [FromRoute] string ruleId,
        CancellationToken cancellationToken)
    {
        var rule = await notificationRuleService.FindRule(ruleId, cancellationToken);
        if (rule == null)
        {
            return TypedResults.NotFound($"rule {ruleId} not found");
        }

        if (rule.Entity != entityId)
        {
            return TypedResults.BadRequest($"bad request: rule {ruleId} does not belong to {entityId} it belongs to {rule.Entity}");
        }

        await notificationRuleService.DeleteAsync(ruleId, cancellationToken);
        return TypedResults.Ok();
    }
    
    [EndpointDescription("Gets all rules that belong to an entity. Does not check entity exists.")]
    private static async Task<Ok<List<NotificationRule>>> FindNotificationRulesForEntity(
        [FromServices] INotificationRuleService notificationRuleService,
        [FromRoute] string entityId,
        CancellationToken cancellationToken)
    {
        var rules = await notificationRuleService.FindByEntity(entityId, cancellationToken);
        return TypedResults.Ok(rules);
    }

    [EndpointDescription("Gets a specific rule for an entity by rule ID")]
    private static async Task<Results<NotFound, Ok<NotificationRule>>> GetNotificationRule(
        [FromServices] INotificationRuleService notificationRuleService,
        [FromRoute] string entityId,
        [FromRoute] string ruleId,
        CancellationToken cancellationToken)
    {
        var rule = await notificationRuleService.FindRule(ruleId, cancellationToken);
        return rule == null ? TypedResults.NotFound() : TypedResults.Ok(rule);
    }
    
    [EndpointDescription("Gets a list of notification that can be triggered by the given entity.")]
    private static async Task<Results<NotFound<string>, Ok<List<NotificationOptions>>>> FindSupportedNotifications(
        [FromServices] IEntitiesService entitiesService,
        [FromRoute] string entityId,
        CancellationToken cancellationToken)
    {
        var entity = await entitiesService.GetEntity(entityId, cancellationToken);
        
        if (entity == null)
        {
            return TypedResults.NotFound($"entity {entityId} not found");
        }

        var supportedNotifications = NotificationOptionLookup.FindOptionsForEntity(entity);
        return TypedResults.Ok(supportedNotifications);
    }
}

public class CreateRuleRequest : IValidatableObject
{
    public required string EventType { get; init; }
    public required List<string> Environments { get; init; }
    public string? SlackChannel { get; init; }
    public bool IsEnabled { get; init; } = true;

    public NotificationRule ToRule(string entityId)
    {
        return new NotificationRule
        {
            EventType = EventType,
            Entity = entityId,
            Environments = Environments,
            SlackChannel = SlackChannel,
            IsEnabled = IsEnabled
        };
    }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!NotificationTypes.All.Contains(EventType))
        {
            yield return new ValidationResult(
                $"invalid eventType {EventType}",
                [nameof(EventType)]
            );
        }
        
        foreach (var env in Environments.Where(env => !CdpEnvironments.Environments.Contains(env)))
        {
            yield return new ValidationResult(
                $"invalid environment name {env}",
                [nameof(Environments)]
            );
        }
        
        if (SlackChannel != null && SlackChannel.StartsWith('#'))
        {
            yield return new ValidationResult(
                "Channels must be provided without a # prefix",
                [nameof(SlackChannel)]
            );
        }
    }
}

public class UpdateRuleRequest : IValidatableObject
{
    public required string EventType { get; init; }
    public required List<string> Environments { get; init; }
    public string? SlackChannel { get; init; }
    public bool IsEnabled { get; init; } = true;

    public NotificationRule ToRule(string entityId, NotificationRule rule)
    {
        return rule with
        {
            EventType = EventType,
            Entity = entityId,
            Environments = Environments,
            SlackChannel = SlackChannel,
            IsEnabled = IsEnabled
        };
    }
    
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (!NotificationTypes.All.Contains(EventType))
        {
            yield return new ValidationResult(
                $"invalid eventType {EventType}",
                [nameof(EventType)]
            );
        }
        
        foreach (var env in Environments.Where(env => !CdpEnvironments.Environments.Contains(env)))
        {
            yield return new ValidationResult(
                $"invalid environment name {env}",
                [nameof(Environments)]
            );
        }

        if (SlackChannel != null && SlackChannel.StartsWith('#'))
        {
            yield return new ValidationResult(
                "Channels must be provided without a # prefix",
                [nameof(SlackChannel)]
            );
        }
    }
}