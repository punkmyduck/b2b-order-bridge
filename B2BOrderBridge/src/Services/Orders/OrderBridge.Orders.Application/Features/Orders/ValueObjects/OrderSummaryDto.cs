using OrderBridge.Orders.Application.ValueObjects;
using OrderBridge.Orders.Domain.Models;
using OrderBridge.Orders.Domain.ValueObjects;

namespace OrderBridge.Orders.Application.Features.Orders.ValueObjects;

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
