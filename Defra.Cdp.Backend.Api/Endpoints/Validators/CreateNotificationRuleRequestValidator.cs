using Defra.Cdp.Backend.Api.Services.Notifications;
using Defra.Cdp.Backend.Api.Utils;
using FluentValidation;

namespace Defra.Cdp.Backend.Api.Endpoints.Validators;

public class CreateNotificationRuleRequestValidator: AbstractValidator<CreateRuleRequest>
{
    public CreateNotificationRuleRequestValidator()
    {
        RuleFor(x => x.EventType)
            .NotEmpty()
            .Must(x => NotificationTypes.All.Contains(x))
            .WithMessage(x => $"Invalid event type {x.EventType}, valid values: { string.Join(",", NotificationTypes.All) }");

        RuleFor(x => x.Environments)
            .NotNull()
            .Must(x => x.TrueForAll(env => CdpEnvironments.Environments.Contains(env)))
            .WithMessage(x => $"Invalid environment(s): {x.Environments.Where(env => !CdpEnvironments.Environments.Contains(env)) }");

        RuleFor(x => x.SlackChannel)
            .Must(x => x == null || !string.IsNullOrWhiteSpace(x))
            .WithMessage("Invalid slack channel");
    }
}
