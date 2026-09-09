using OrderBridge.Domain.Models;
using OrderBridge.Domain.ValueObjects;
using Shared.Domain;

namespace OrderBridge.Domain.Events;

public class OrderCancelledEvent : IDomainEvent
{
    public Guid EventId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public Guid OrderId { get; init; }

    public OrderStatus OrderStatus { get; init; }

    public PaymentStatus PaymentStatus { get; init; }

    public OrderCancelledEvent(
        Guid eventId,
        DateTimeOffset occurredAt,
        Order order)
    {
        EventId = Guard.NotEmpty(eventId, nameof(EventId));
        OccurredAt = occurredAt;
        OrderId = order.Id;
        OrderStatus = order.OrderStatus;
        PaymentStatus = order.PaymentStatus;
    }
}
