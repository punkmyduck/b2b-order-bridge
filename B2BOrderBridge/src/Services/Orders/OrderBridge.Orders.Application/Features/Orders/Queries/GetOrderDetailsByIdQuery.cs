using OrderBridge.Orders.Application.Features.Orders.Repositories;
using OrderBridge.Orders.Application.Features.Orders.ValueObjects;
using Shared.Application.Messaging;
using Shared.Application.Results;

namespace OrderBridge.Orders.Application.Features.Orders.Queries;

public sealed record GetOrderDetailsByIdQuery(Guid Id) : IQuery<OrderDetailsDto>;

public sealed class GetOrderDetailsByIdQueryHandler(IOrderReadRepository repository)
    : IQueryHandler<GetOrderDetailsByIdQuery, OrderDetailsDto>
{
    public async Task<Result<OrderDetailsDto>> Handle(
        GetOrderDetailsByIdQuery query,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetDetailsByIdAsync(query.Id, cancellationToken);

        return order is null
            ? new Error(
                "Order.NotFound",
                $"Order '{query.Id}' was not found.",
                ErrorType.NotFound)
            : order;
    }
}
