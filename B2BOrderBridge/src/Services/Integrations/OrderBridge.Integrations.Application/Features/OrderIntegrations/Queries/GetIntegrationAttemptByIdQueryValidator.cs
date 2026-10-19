using FluentValidation;

namespace OrderBridge.Integrations.Application.Features.OrderIntegrations.Queries;

public sealed class GetIntegrationAttemptByIdQueryValidator
    : AbstractValidator<GetIntegrationAttemptByIdQuery>
{
    public GetIntegrationAttemptByIdQueryValidator()
    {
        RuleFor(query => query.Id)
            .NotEmpty()
            .WithMessage("Integration attempt ID is required.");
    }
}
