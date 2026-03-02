using Defra.Cdp.Backend.Api.Services.Notifications;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class NotificationEndpoints
{
   public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
   {
       app.MapPost("/entities/{entity}/notifications", CreateNotification);
       app.MapGet("/entities/{entity}/notifications", FindNotificationRulesForEntity);
       app.MapGet("/entities/{entity}/notifications/{ruleId}", GetNotificationRule).WithName("GetNotificationRule");
    }
    
    private static async Task<IResult> CreateNotification(
        IValidator<CreateNotificationRuleRequest> validator,
        [FromServices] INotificationRuleService notificationRuleService, 
        [FromBody] CreateNotificationRuleRequest request,
        CancellationToken cancellationToken)
    {
        var validationResult = await validator.ValidateAsync(request, cancellationToken);

        if (!validationResult.IsValid) 
        {
            return Results.ValidationProblem(validationResult.ToDictionary());
        }
        
        var rule = request.ToRule();
        await notificationRuleService.SaveAsync(rule, cancellationToken);
        
        return Results.CreatedAtRoute(
            routeName: "GetNotificationRule", 
            routeValues: new { entity = rule.Entity, ruleId = rule.RuleId}
        );
    }
    
    
    private static async Task<IResult> FindNotificationRulesForEntity(
        [FromServices] INotificationRuleService notificationRuleService, 
        [FromRoute] string entity,
        CancellationToken cancellationToken)
    {
        var rules = await notificationRuleService.FindByEntity(entity, cancellationToken);
        return Results.Ok(rules);
    }
    
    private static async Task<IResult> GetNotificationRule(
        [FromServices] INotificationRuleService notificationRuleService, 
        [FromRoute] string ruleId,
        CancellationToken cancellationToken)
    {
        var rule = await notificationRuleService.FindRule(ruleId, cancellationToken);
        return rule == null ? Results.NotFound() : Results.Ok(rule);
    }
}

public class CreateNotificationRuleRequest
{
    public required string EventType { get; init; }
    public required string Entity { get; init; }
    public string? Environment { get; init; }
    public string? SlackChannel { get; init; }
    public bool IsEnabled { get; init; } = true;

    public NotificationRule ToRule()
    {
        return new NotificationRule
        {
            EventType = EventType,
            Entity = Entity, 
            Environment = Environment,
            SlackChannel = SlackChannel, 
            IsEnabled = IsEnabled
        };
    }
}
