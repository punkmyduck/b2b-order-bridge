using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.Models;

namespace OrderBridge.Application.Features.OrderIntegrations.ValueObjects;

public sealed record OrderExecutionSnapshot(
    Guid Id,
    string SourceSystem,
    string ExternalOrderId,
    CustomerSnapshotDto Customer,
    string Currency,
    decimal TotalAmount,
    IReadOnlyList<OrderLineDto> Lines)
{
    public static OrderExecutionSnapshot FromDomain(Order domain) => new(
        domain.Id,
        domain.SourceSystem,
        domain.ExternalOrderId,
        CustomerSnapshotDto.FromDomain(domain.Customer),
        domain.Currency,
        domain.TotalAmount,
        domain.Lines
            .Select(line => new OrderLineDto(line.Sku, line.Name, line.Quantity, line.UnitPrice))
            .ToList()
            .AsReadOnly());
}
