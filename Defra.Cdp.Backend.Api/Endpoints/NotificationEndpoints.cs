using System.Text.Json.Serialization;
using Defra.Cdp.Backend.Api.Services.Notifications;
using Microsoft.AspNetCore.Mvc;

namespace Defra.Cdp.Backend.Api.Endpoints;

public static class NotificationEndpoints
{
   public static void MapNotificationEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/notifications", CreateNotification);
    }
    
    private static async Task<IResult> CreateNotification(
        [FromServices] INotificationRuleService notificationRuleService, 
        [FromBody] CreateNotificationRuleRequest request,
        CancellationToken cancellationToken)
    {
        var rule = request.ToRule();
        await notificationRuleService.SaveAsync(rule, cancellationToken);
        return Results.Created($"/notifications/{rule.RuleId}", rule);
    }
}

public class CreateNotificationRuleRequest
{
    public required string EventType { get; init; }
    public required string Entity { get; init; }
    public string? SlackChannel { get; init; }
    public bool IsEnabled { get; init; } = true;
    public Dictionary<string, string> Conditions { get; init; } = new();

    public NotificationRule ToRule()
    {
        return new NotificationRule
        {
            EventType = EventType,
            Entity = Entity, 
            SlackChannel = SlackChannel, 
            IsEnabled = IsEnabled, 
            Conditions = Conditions
        };
    }
}
