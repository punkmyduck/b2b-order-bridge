using OrderBridge.Orders.Domain.Models;
using OrderBridge.Orders.Domain.ValueObjects;

namespace OrderBridge.Orders.Application.Features.Orders.ValueObjects;

public record CreateOrderResponse(
    Guid Id,
    string SourceSystem,
    string ExternalOrderId,
    OrderStatus OrderStatus,
    PaymentStatus PaymentStatus,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAt)
{
    public static CreateOrderResponse FromDomain(Order order) => new(
        order.Id,
        order.SourceSystem,
        order.ExternalOrderId,
        order.OrderStatus,
        order.PaymentStatus,
        order.TotalAmount,
        order.Currency,
        order.CreatedAt);
}