namespace OrderBridge.Application.ValueObjects;

public record OrderLineDto(
    string Sku,
    string Name,
    decimal Quantity,
    decimal UnitPrice);