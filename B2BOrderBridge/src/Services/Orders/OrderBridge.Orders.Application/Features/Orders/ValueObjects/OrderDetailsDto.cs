using OrderBridge.Orders.Application.ValueObjects;
using OrderBridge.Orders.Domain.ValueObjects;

namespace OrderBridge.Orders.Application.Features.Orders.ValueObjects;

public sealed record OrderDetailsDto(
    Guid Id,
    string SourceSystem,
    string ExternalOrderId,
    CustomerSnapshotDto Customer,
    OrderStatus OrderStatus,
    PaymentStatus PaymentStatus,
    decimal TotalAmount,
    string Currency,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    IReadOnlyList<OrderLineDetailsDto> Lines);
