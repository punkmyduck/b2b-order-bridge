using OrderBridge.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.ValueObjects;

namespace OrderBridge.Application.Features.Orders.ValueObjects;

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
    IReadOnlyList<OrderLineDetailsDto> Lines,
    IReadOnlyList<OrderIntegrationDto> Integrations);
