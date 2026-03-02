using Defra.Cdp.Backend.Api.Services.Notifications;
using Defra.Cdp.Backend.Api.Utils;
using FluentValidation;

namespace Defra.Cdp.Backend.Api.Endpoints.Validators;

public class CreateNotificationRuleRequestValidator: AbstractValidator<CreateNotificationRuleRequest>
{
    public CreateNotificationRuleRequestValidator()
    {
        RuleFor(x => x.EventType)
            .NotEmpty()
            .Must(x => NotificationTypes.All.Contains(x))
            .WithMessage(x => $"Supported event type {x.EventType}");

        RuleFor(x => x.Environment)
            .Must(x => x == null || CdpEnvironments.Environments.Contains(x))
            .WithMessage(x => $"Invalid environment: {x.Environment}");

        RuleFor(x => x.SlackChannel)
            .Must(x => x == null || !string.IsNullOrWhiteSpace(x))
            .WithMessage("Invalid slack channel");

        RuleFor(x => x.Entity).NotEmpty();
    }
}
