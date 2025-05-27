using Defra.Cdp.Backend.Api.Models;
using FluentValidation;

namespace Defra.Cdp.Backend.Api.Endpoints.Validators;

public class RequestedUndeploymentValidator : AbstractValidator<RequestedUndeployment>
{
    public RequestedUndeploymentValidator()
    {
        RuleFor(x => x.Service).NotNull().NotEmpty();
        RuleFor(x => x.Environment).NotNull().NotEmpty();
    }
}