using OrderBridge.Application.ValueObjects;

namespace OrderBridge.Presentation.Requests;

public sealed record CreateOrderRequest(
    string SourceSystem,
    string ExternalOrderId,
    CustomerSnapshotDto Customer,
    string Currency,
    IReadOnlyCollection<OrderLineDto> Lines);
