using OrderBridge.Orders.Domain.Models;
using OrderBridge.Orders.Domain.ValueObjects;
using Shared.Domain;

namespace OrderBridge.Orders.Domain.Events;

/// <summary>An immutable snapshot of the business fact, without Kafka, routing or wire-format metadata.</summary>
public sealed record OrderCreatedDomainEvent : IDomainEvent
{
    public Guid EventId { get; }

    public DateTimeOffset OccurredAt { get; }

    public Guid OrderId { get; }

    public string SourceSystem { get; }

    public string ExternalOrderId { get; }

    public CustomerSnapshot Customer { get; }

    public string Currency { get; }

    public decimal TotalAmount { get; }

    public IReadOnlyList<OrderCreatedLineSnapshot> Lines { get; }

    public OrderCreatedDomainEvent(
        Guid eventId,
        DateTimeOffset occurredAt,
        Order order)
    {
        ArgumentNullException.ThrowIfNull(order);

        EventId = Guard.NotEmpty(eventId, nameof(eventId));
        OccurredAt = occurredAt;
        OrderId = order.Id;
        SourceSystem = order.SourceSystem;
        ExternalOrderId = order.ExternalOrderId;
        Customer = order.Customer;
        Currency = order.Currency;
        TotalAmount = order.TotalAmount;

        var lines = order.Lines
            .Select(line => new OrderCreatedLineSnapshot(
                line.Id,
                line.Sku,
                line.Name,
                line.Quantity,
                line.UnitPrice,
                line.LineTotal))
            .ToArray();

        Lines = Array.AsReadOnly(lines);
    }
}

public sealed record OrderCreatedLineSnapshot(
    Guid Id,
    string Sku,
    string Name,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);
