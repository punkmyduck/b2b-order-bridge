namespace OrderBridge.Orders.Application.Features.Orders.ValueObjects;

public sealed record OrderLineDetailsDto(
    Guid Id,
    string Sku,
    string Name,
    decimal Quantity,
    decimal UnitPrice,
    decimal LineTotal);
