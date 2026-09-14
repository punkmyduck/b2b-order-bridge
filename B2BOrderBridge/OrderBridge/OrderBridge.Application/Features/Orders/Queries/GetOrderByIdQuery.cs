using OrderBridge.Application.Features.Orders.Repositories;
using OrderBridge.Application.Features.Orders.ValueObjects;
using Shared.Application.Messaging;
using Shared.Application.Results;

namespace OrderBridge.Application.Features.Orders.Queries;

public sealed record GetOrderByIdQuery(Guid Id) : IQuery<OrderSummaryDto>;

public sealed class GetOrderByIdQueryHandler(IOrderReadRepository orderRepository)
    : IQueryHandler<GetOrderByIdQuery, OrderSummaryDto>
{
    public async Task<Result<OrderSummaryDto>> Handle(
        GetOrderByIdQuery query,
        CancellationToken cancellationToken)
    {
        var order = await orderRepository.GetSummaryByIdAsync(query.Id, cancellationToken);

        if (order is null)
        {
            return new Error(
                "Order.NotFound",
                $"Order '{query.Id}' was not found.",
                ErrorType.NotFound);
        }

        return order;
    }
}
