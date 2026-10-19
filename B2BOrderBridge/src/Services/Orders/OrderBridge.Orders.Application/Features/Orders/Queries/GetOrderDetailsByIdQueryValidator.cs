using FluentValidation;

namespace OrderBridge.Orders.Application.Features.Orders.Queries;

public sealed class GetOrderDetailsByIdQueryValidator
    : AbstractValidator<GetOrderDetailsByIdQuery>
{
    public GetOrderDetailsByIdQueryValidator()
    {
        RuleFor(query => query.Id)
            .NotEmpty()
            .WithMessage("Order ID is required.");
    }
}
