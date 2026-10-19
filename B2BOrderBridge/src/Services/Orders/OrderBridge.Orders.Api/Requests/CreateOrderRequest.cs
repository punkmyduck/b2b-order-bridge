using OrderBridge.Orders.Application.ValueObjects;

namespace OrderBridge.Orders.Api.Requests;

public sealed record CreateOrderRequest(
    string SourceSystem,
    string ExternalOrderId,
    CustomerSnapshotDto Customer,
    string Currency,
    IReadOnlyCollection<OrderLineDto> Lines);
