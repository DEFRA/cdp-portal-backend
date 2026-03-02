using Defra.Cdp.Backend.Api.Services.Notifications;
using FluentValidation;

namespace Defra.Cdp.Backend.Api.Endpoints.Validators;

public class CreateNotificationRuleRequestValidator: AbstractValidator<CreateNotificationRuleRequest>
{
    public CreateNotificationRuleRequestValidator()
    {
        RuleFor(x => x.EventType)
            .NotEmpty()
            .Must(x => NotificationEventTypes.Map.ContainsKey(x))
            .WithMessage(x => $"Supported event type {x.EventType}");
        
        RuleFor(x => x.Entity).NotEmpty();

        RuleFor(x => x.Conditions)
            .Must((request, conditions) =>
                OnlyContainsAllowedKeys(conditions, NotificationEventTypes.Map[request.EventType].AllowedParams))
            .WithMessage(x =>
                $"Invalid condition keys for {x.EventType}. Allowed keys are: {string.Join(", ", NotificationEventTypes.Map[x.EventType].AllowedParams)}");
    }
    
    private static bool OnlyContainsAllowedKeys(Dictionary<string, string> conditions, string[] allowedKeys)
    {
        if (conditions.Count == 0) 
            return true;
        return conditions.Keys.All(key => 
            allowedKeys.Contains(key, StringComparer.OrdinalIgnoreCase));
    }
}