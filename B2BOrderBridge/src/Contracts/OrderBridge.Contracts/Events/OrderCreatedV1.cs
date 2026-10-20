using System.Text.Json;
using Shared.Messaging;
using Shared.Messaging.Abstractions.Events;

namespace OrderBridge.Contracts.Events;

// Only the business payload. Common metadata belongs to IntegrationEvent<T>.
public sealed record OrderCreatedV1(
    string ProcessingScenario,
    OrderSnapshotV1 Order);

public sealed record OrderSnapshotV1(
    Guid Id,
    string SourceSystem,
    string ExternalOrderId,
    CustomerSnapshotV1 Customer,
    string Currency,
    decimal TotalAmount,
    IReadOnlyList<OrderLineV1> Lines);

public sealed record CustomerSnapshotV1(
    string CompanyName,
    string TaxId,
    string ContactEmail);

public sealed record OrderLineV1(
    Guid Id,
    string Sku,
    string Name,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);

public static class OrderCreatedEvent
{
    public static EventDefinition<OrderCreatedV1> Definition { get; } = new(
        "order.created.v1",
        1,
        "orderbridge.orders.created.v1");

/// <summary>Read events already stored in Kafka before the envelope was introduced.</summary>
    public static IntegrationEvent<OrderCreatedV1> ReadLegacy(JsonElement json)
    {
        var old = json.Deserialize<LegacyOrderCreatedV1>(new JsonSerializerOptions(JsonSerializerDefaults.Web));

        if (old is null || old.Order is null)
        {
            throw new InvalidEventException("The legacy order event is incomplete.");
        }

        return IntegrationEvent<OrderCreatedV1>.Create(
            Definition,
            new(old.ProcessingScenario, old.Order),
            "orders",
            old.Order.Id.ToString("N"),
            old.OccurredAt,
            idempotencyKey: old.EventId.ToString("N"),
            eventId: old.EventId);
    }

    private sealed record LegacyOrderCreatedV1(
        Guid EventId,
        DateTimeOffset OccurredAt,
        string ProcessingScenario,
        OrderSnapshotV1 Order);
}
