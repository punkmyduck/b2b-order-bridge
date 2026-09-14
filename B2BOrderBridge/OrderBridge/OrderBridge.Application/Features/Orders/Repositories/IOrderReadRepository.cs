using OrderBridge.Application.Features.Orders.ValueObjects;
using OrderBridge.Domain.Models;
using Shared.Application.Persistence;

namespace OrderBridge.Application.Features.Orders.Repositories;

public interface IOrderReadRepository : IReadRepository<Order, Guid>
{
    Task<OrderSummaryDto?> GetSummaryByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default);
}
