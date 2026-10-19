using OrderBridge.Orders.Application.Features.Orders.Repositories;
using OrderBridge.Orders.Application.Features.Orders.ValueObjects;
using OrderBridge.Orders.Application.ValueObjects;
using Shared.Application.Messaging;
using Shared.Application.Pagination;
using Shared.Application.Results;

namespace OrderBridge.Orders.Application.Features.Orders.Queries;

public sealed record GetPagedOrdersQuery(
    PaginationRequest Pagination,
    OrdersFilter? Filter = null,
    OrdersSortBy SortBy = OrdersSortBy.CreatedAt,
    SortDirection SortDirection = SortDirection.Descending)
    : IQuery<PaginationResult<OrderSummaryDto>>;

public sealed class GetPagedOrdersQueryHandler(IOrderReadRepository orderReadRepository)
    : IQueryHandler<GetPagedOrdersQuery, PaginationResult<OrderSummaryDto>>
{
    public async Task<Result<PaginationResult<OrderSummaryDto>>> Handle(
        GetPagedOrdersQuery query,
        CancellationToken cancellationToken)
    {
        return await orderReadRepository.GetPagedAsync(
            query.Pagination,
            query.Filter,
            query.SortBy,
            query.SortDirection,
            cancellationToken);
    }
}
