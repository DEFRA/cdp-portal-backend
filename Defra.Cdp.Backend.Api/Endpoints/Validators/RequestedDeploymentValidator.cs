using Defra.Cdp.Backend.Api.Models;
using FluentValidation;

namespace Defra.Cdp.Backend.Api.Endpoints.Validators;

public class RequestedDeploymentValidator : AbstractValidator<RequestedDeployment>
{
    public RequestedDeploymentValidator()
    {
        RuleFor(x => x.Service).NotNull().NotEmpty();
        RuleFor(x => x.Version).NotNull().NotEmpty();
        RuleFor(x => x.Environment).NotNull().NotEmpty();
    }
}