using System.Text.Json.Serialization;

namespace OrderBridge.Contracts.Events;

// Versioned wire contract: no references to domain entities, EF or mediator requests.
public sealed record OrderCreatedV1(
    [property: JsonRequired] Guid EventId,
    [property: JsonRequired] DateTimeOffset OccurredAt,
    [property: JsonRequired] string ProcessingScenario,
    [property: JsonRequired] OrderSnapshotV1 Order)
{
    public const string EventType = "order.created.v1";
}

public sealed record OrderSnapshotV1(
    Guid Id, string SourceSystem, string ExternalOrderId,
    CustomerSnapshotV1 Customer, string Currency, decimal TotalAmount,
    IReadOnlyList<OrderLineV1> Lines);

public sealed record CustomerSnapshotV1(string CompanyName, string TaxId, string ContactEmail);
public sealed record OrderLineV1(Guid Id, string Sku, string Name, decimal Quantity, decimal UnitPrice, decimal LineTotal);
