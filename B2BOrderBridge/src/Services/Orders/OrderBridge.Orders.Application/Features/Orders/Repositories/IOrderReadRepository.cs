using OrderBridge.Orders.Application.Features.Orders.ValueObjects;
using OrderBridge.Orders.Application.ValueObjects;
using OrderBridge.Orders.Domain.Models;
using Shared.Application.Pagination;
using Shared.Application.Persistence;

namespace OrderBridge.Orders.Application.Features.Orders.Repositories;

public interface IOrderReadRepository : IReadRepository<Order, Guid>
{
    Task<OrderSummaryDto?> GetSummaryByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<OrderDetailsDto?> GetDetailsByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);

    Task<PaginationResult<OrderSummaryDto>> GetPagedAsync(
        PaginationRequest paginationRequest,
        OrdersFilter? filter = null,
        OrdersSortBy sortBy = OrdersSortBy.CreatedAt,
        SortDirection sortDirection = SortDirection.Descending,
        CancellationToken cancellationToken = default);
}
