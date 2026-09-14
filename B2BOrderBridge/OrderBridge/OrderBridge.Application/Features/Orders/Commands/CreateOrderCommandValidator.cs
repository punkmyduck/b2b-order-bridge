using FluentValidation;
using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.Models;
using OrderBridge.Domain.ValueObjects;

namespace OrderBridge.Application.Features.Orders.Commands;

public sealed class CreateOrderCommandValidator : AbstractValidator<CreateOrderCommand>
{
    public CreateOrderCommandValidator()
    {
        RuleFor(command => command.SourceSystem)
            .Must(value => Required(value, Order.SourceSystemMaxLength))
            .WithMessage($"Source system is required and must not exceed {Order.SourceSystemMaxLength} characters after trimming.");
        RuleFor(command => command.ExternalOrderId)
            .Must(value => Required(value, Order.ExternalOrderIdMaxLength))
            .WithMessage($"External order ID is required and must not exceed {Order.ExternalOrderIdMaxLength} characters after trimming.");
        RuleFor(command => command.Currency)
            .Must(value => value is not null && CurrencyCodes.Supported.Contains(value.Trim().ToUpperInvariant()))
            .WithMessage("A supported currency code is required.");
        RuleFor(command => command.Customer).NotNull().WithMessage("Customer is required.");
        When(command => command.Customer is not null, () =>
        {
            RuleFor(command => command.Customer.CompanyName)
                .Must(value => Required(value, CustomerSnapshot.CompanyNameMaxLength))
                .WithMessage($"Company name is required and must not exceed {CustomerSnapshot.CompanyNameMaxLength} characters after trimming.");
            RuleFor(command => command.Customer.TaxId)
                .Must(value => Required(value, CustomerSnapshot.TaxIdMaxLength))
                .WithMessage($"Tax ID is required and must not exceed {CustomerSnapshot.TaxIdMaxLength} characters after trimming.");
            RuleFor(command => command.Customer.ContactEmail)
                .Cascade(CascadeMode.Stop)
                .Must(value => Required(value, CustomerSnapshot.ContactEmailMaxLength))
                .WithMessage($"Contact email is required and must not exceed {CustomerSnapshot.ContactEmailMaxLength} characters after trimming.")
                .EmailAddress()
                .WithMessage("Contact email must be a valid email address.");
        });
        RuleFor(command => command.Lines).NotEmpty().WithMessage("At least one order line is required.");
        When(command => command.Lines is not null, () =>
        {
            RuleForEach(command => command.Lines).NotNull().WithMessage("Order line must not be null.");
            RuleForEach(command => command.Lines).SetValidator(new OrderLineInputValidator());
            RuleFor(command => command.Lines).Must(TotalFitsDecimal)
                .WithMessage("Order amounts exceed the supported decimal range.");
        });
    }

    private static bool Required(string? value, int maximum)
        => !string.IsNullOrWhiteSpace(value) && value.Trim().Length <= maximum;

    private static bool TotalFitsDecimal(IReadOnlyCollection<OrderLineDto> lines)
    {
        try
        {
            decimal total = 0;
            foreach (var line in lines)
            {
                if (line is null || line.Quantity <= 0 || line.UnitPrice < 0)
                    continue;
                total += decimal.Round(line.Quantity * line.UnitPrice, 2, MidpointRounding.AwayFromZero);
            }
            return true;
        }
        catch (OverflowException) { return false; }
    }

    private sealed class OrderLineInputValidator : AbstractValidator<OrderLineDto>
    {
        public OrderLineInputValidator()
        {
            RuleFor(x => x.Sku).Must(x => Required(x, OrderLine.SkuMaxLength))
                .WithMessage("SKU is required and must not exceed the allowed length.");
            RuleFor(x => x.Name).Must(x => Required(x, OrderLine.NameMaxLength))
                .WithMessage("Item name is required and must not exceed the allowed length.");
            RuleFor(x => x.Quantity).GreaterThan(0).WithMessage("Quantity must be greater than zero.");
            RuleFor(x => x.UnitPrice).GreaterThanOrEqualTo(0).WithMessage("Unit price must not be negative.");
            RuleFor(x => x.UnitPrice).Must(x => decimal.Round(x, 2) == x)
                .WithMessage("Unit price must have no more than two fractional digits.");
        }
    }
}
