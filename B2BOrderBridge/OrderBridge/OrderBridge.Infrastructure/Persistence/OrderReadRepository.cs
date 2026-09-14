using Microsoft.EntityFrameworkCore;
using OrderBridge.Application.Features.Orders.Repositories;
using OrderBridge.Application.Features.Orders.ValueObjects;
using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.Models;

namespace OrderBridge.Infrastructure.Persistence;

public sealed class OrderReadRepository(OrderBridgeDbContext context)
    : EfRepository<Order, Guid>(context), IOrderReadRepository
{
    public Task<OrderSummaryDto?> GetSummaryByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => Context.Orders
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Where(order => order.Id == id)
            .Select(order => new OrderSummaryDto(
                order.Id,
                order.SourceSystem,
                order.ExternalOrderId,
                new CustomerSnapshotDto(
                    order.Customer.CompanyName,
                    order.Customer.TaxId,
                    order.Customer.ContactEmail),
                order.OrderStatus,
                order.PaymentStatus,
                order.TotalAmount,
                order.Currency,
                order.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken);
}
