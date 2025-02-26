using Defra.Cdp.Backend.Api.Models;
using FluentValidation;

namespace Defra.Cdp.Backend.Api.Endpoints.Validators;

public class RequestedAnnotationValidator : AbstractValidator<RequestedAnnotation>
{
    public RequestedAnnotationValidator()
    {
        RuleFor(x => x.Title).NotNull().NotEmpty();
        RuleFor(x => x.Description).NotNull().NotEmpty();
    }
}