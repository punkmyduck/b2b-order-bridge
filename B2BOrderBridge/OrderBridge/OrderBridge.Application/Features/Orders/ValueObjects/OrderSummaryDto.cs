using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.Models;
using OrderBridge.Domain.ValueObjects;

namespace OrderBridge.Application.Features.Orders.ValueObjects;

public sealed record OrderSummaryDto(
    Guid Id,
    string SourceSystem,
    string ExternalOrderId,
    CustomerSnapshotDto Customer,
    OrderStatus OrderStatus,
    PaymentStatus PaymentStatus,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAt)
{
    public static OrderSummaryDto FromDomain(Order order) => new(
        order.Id,
        order.SourceSystem,
        order.ExternalOrderId,
        CustomerSnapshotDto.FromDomain(order.Customer),
        order.OrderStatus,
        order.PaymentStatus,
        order.TotalAmount,
        order.Currency,
        order.CreatedAt);
}
