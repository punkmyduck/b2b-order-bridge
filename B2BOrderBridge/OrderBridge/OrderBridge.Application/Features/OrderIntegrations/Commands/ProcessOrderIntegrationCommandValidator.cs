using FluentValidation;

namespace OrderBridge.Application.Features.OrderIntegrations.Commands;

public sealed class ProcessOrderIntegrationCommandValidator
    : AbstractValidator<ProcessOrderIntegrationCommand>
{
    public ProcessOrderIntegrationCommandValidator()
    {
        RuleFor(command => command.IntegrationId)
            .NotEmpty()
            .WithMessage("Order integration ID is required.");
    }
}
