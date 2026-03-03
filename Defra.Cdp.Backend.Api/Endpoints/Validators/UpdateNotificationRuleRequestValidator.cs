using Defra.Cdp.Backend.Api.Services.Notifications;
using Defra.Cdp.Backend.Api.Utils;
using FluentValidation;

namespace Defra.Cdp.Backend.Api.Endpoints.Validators;

public class UpdateNotificationRuleRequestValidator: AbstractValidator<UpdateRuleRequest>
{
    public UpdateNotificationRuleRequestValidator()
    {
        RuleFor(x => x.EventType)
            .NotEmpty()
            .Must(x => NotificationTypes.All.Contains(x))
            .WithMessage(x => $"Invalid event type {x.EventType}, valid values: { string.Join(",", NotificationTypes.All) }");

        RuleFor(x => x.Environment)
            .Must(x => x == null || CdpEnvironments.Environments.Contains(x))
            .WithMessage(x => $"Invalid environment: {x.Environment}, valid values: {string.Join(",", CdpEnvironments.Environments)} ");

        RuleFor(x => x.SlackChannel)
            .Must(x => x == null || !string.IsNullOrWhiteSpace(x))
            .WithMessage("Invalid slack channel");
    }
}
