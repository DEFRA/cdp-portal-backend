using Defra.Cdp.Backend.Api.Services.Notifications;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/entities/{entityId}/notifications", CreateNotification);
        app.MapGet("/entities/{entityId}/notifications", FindNotificationRulesForEntity);
        app.MapGet("/entities/{entityId}/notifications/{ruleId}", GetNotificationRule).WithName("GetNotificationRule");
        app.MapPut("/entities/{entityId}/notifications/{ruleId}", UpdateNotification);
        app.MapDelete("/entities/{entityId}/notifications/{ruleId}", DeleteNotification);
    }

    private static async Task<IResult> CreateNotification(
        [FromServices] IValidator<CreateRuleRequest> validator,
        [FromServices] INotificationRuleService notificationRuleService,
        [FromRoute] string entityId,
        [FromBody] CreateRuleRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }

        var rule = request.ToRule(entityId);
        await notificationRuleService.SaveAsync(rule, cancellationToken);

        return Results.CreatedAtRoute(
            routeName: "GetNotificationRule",
            routeValues: new { entity = rule.Entity, ruleId = rule.RuleId }
        );
    }

    private static async Task<IResult> UpdateNotification(
        [FromServices] IValidator<UpdateRuleRequest> validator,
        [FromServices] INotificationRuleService notificationRuleService,
        [FromRoute] string entityId,
        [FromRoute] string ruleId,
        [FromBody] UpdateRuleRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);
        if (!validationResult.IsValid)
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }
        
        var rule = await notificationRuleService.FindRule(ruleId, cancellationToken);
        if (rule == null)
        {
            return Results.NotFound($"rule {ruleId} not found");
        }
        
        await notificationRuleService.UpdateAsync(request.ToRule(entityId, rule), cancellationToken);
        return Results.Ok();
    }

    private static async Task<IResult> DeleteNotification(
        [FromServices] INotificationRuleService notificationRuleService,
        [FromRoute] string entityId,
        [FromRoute] string ruleId,
        CancellationToken cancellationToken)
    {
        var rule = await notificationRuleService.FindRule(ruleId, cancellationToken);
        if (rule == null)
        {
            return Results.NotFound($"rule {ruleId} not found");
        }

        if (rule.Entity != entityId)
        {
            return Results.BadRequest($"bad request: rule {ruleId} does not belong to {entityId} it belongs to {rule.Entity}");
        }

        await notificationRuleService.DeleteAsync(ruleId, cancellationToken);
        return Results.Ok();
    }
    
    private static async Task<IResult> FindNotificationRulesForEntity(
        [FromServices] INotificationRuleService notificationRuleService,
        [FromRoute] string entityId,
        CancellationToken cancellationToken)
    {
        var rules = await notificationRuleService.FindByEntity(entityId, cancellationToken);
        return Results.Ok(rules);
    }

    private static async Task<IResult> GetNotificationRule(
        [FromServices] INotificationRuleService notificationRuleService,
        [FromRoute] string entityId,
        [FromRoute] string ruleId,
        CancellationToken cancellationToken)
    {
        var rule = await notificationRuleService.FindRule(ruleId, cancellationToken);
        return rule == null ? Results.NotFound() : Results.Ok(rule);
    }
}

public class CreateRuleRequest
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
}

public class UpdateRuleRequest
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
}