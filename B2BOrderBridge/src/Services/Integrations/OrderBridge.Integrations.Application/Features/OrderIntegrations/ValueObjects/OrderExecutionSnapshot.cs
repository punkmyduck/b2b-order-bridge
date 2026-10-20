using OrderBridge.Integrations.Application.ValueObjects;
using OrderBridge.Contracts.Events;

namespace OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;

public sealed record OrderExecutionSnapshot(
    Guid Id,
    string SourceSystem,
    string ExternalOrderId,
    CustomerSnapshotDto Customer,
    string Currency,
    decimal TotalAmount,
    IReadOnlyList<OrderLineDto> Lines)
{
    public static OrderExecutionSnapshot FromEvent(OrderSnapshotV1 order) => new(
        order.Id,
        order.SourceSystem,
        order.ExternalOrderId,
        new CustomerSnapshotDto(
            order.Customer.CompanyName,
            order.Customer.TaxId,
            order.Customer.ContactEmail),
        order.Currency,
        order.TotalAmount,
        order.Lines.Select(line => new OrderLineDto(
            line.Sku,
            line.Name,
            line.Quantity,
            line.UnitPrice))
            .ToArray());
}
