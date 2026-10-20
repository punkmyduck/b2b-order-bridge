using MediatR;
using OrderBridge.Contracts.Events;
using OrderBridge.Orders.Application.ValueObjects;
using OrderBridge.Orders.Domain.Events;
using Shared.Application.Events;
using Shared.Messaging.Abstractions.Events;
using Shared.Messaging.Abstractions.Publishing;

namespace OrderBridge.Orders.Application.Messaging;

/// <summary>Projects an internal business fact into the public integration contract before committing the unit of work.</summary>
public sealed class OrderCreatedDomainEventHandler(IEventOutbox outbox)
    : INotificationHandler<DomainEventNotification<OrderCreatedDomainEvent>>
{
    public Task Handle(
        DomainEventNotification<OrderCreatedDomainEvent> notification,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var domainEvent = notification.Event;

        var customer = new CustomerSnapshotV1(
            domainEvent.Customer.CompanyName,
            domainEvent.Customer.TaxId,
            domainEvent.Customer.ContactEmail);

        var lines = domainEvent.Lines
            .Select(line => new OrderLineV1(
                line.Id,
                line.Sku,
                line.Name,
                line.Quantity,
                line.UnitPrice,
                line.LineTotal))
            .ToArray();

        var order = new OrderSnapshotV1(
            domainEvent.OrderId,
            domainEvent.SourceSystem,
            domainEvent.ExternalOrderId,
            customer,
            domainEvent.Currency,
            domainEvent.TotalAmount,
            lines);

        var payload = new OrderCreatedV1(
            OrderProcessingScenario.FullCycle.ToString(),
            order);

        var integrationEvent = IntegrationEvent<OrderCreatedV1>.Create(
            OrderCreatedEvent.Definition,
            payload,
            source: "orders",
            orderingKey: domainEvent.OrderId.ToString("N"),
            occurredAt: domainEvent.OccurredAt,
            idempotencyKey: $"order.created:{domainEvent.OrderId:N}",
            eventId: domainEvent.EventId);

        outbox.Add(OrderCreatedEvent.Definition, integrationEvent);

        return Task.CompletedTask;
    }
}
