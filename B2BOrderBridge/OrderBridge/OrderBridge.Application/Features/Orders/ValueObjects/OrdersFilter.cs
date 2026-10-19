using OrderBridge.Domain.ValueObjects;

namespace OrderBridge.Application.Features.Orders.ValueObjects;

public record OrdersFilter(
    Guid? Id = null,
    string? SourceSystem = null,
    string? ExternalOrderId = null,
    string? CustomerName = null,
    OrderStatus? OrderStatus = null,
    PaymentStatus? PaymentStatus = null,
    decimal? TotalAmountFrom = null,
    decimal? TotalAmountTo = null,
    string? Currency = null,
    DateTimeOffset? CreatedAtFrom = null,
    DateTimeOffset? CreatedAtTo = null,
    string? SearchTerm = null);
