using FluentValidation;

namespace OrderBridge.Application.Features.Orders.Queries;

public sealed class GetOrderByIdQueryValidator : AbstractValidator<GetOrderByIdQuery>
{
    public GetOrderByIdQueryValidator()
    {
        RuleFor(query => query.Id)
            .NotEmpty()
            .WithMessage("Order ID is required.");
    }
}
