using System.ComponentModel.DataAnnotations;
using OrderBridge.Orders.Application.Features.Orders.ValueObjects;
using OrderBridge.Orders.Application.ValueObjects;
using OrderBridge.Orders.Domain.ValueObjects;
using Shared.Application.Pagination;

namespace OrderBridge.Orders.Api.Requests;

public sealed class GetPagedOrdersRequest
{
    [Range(1, int.MaxValue)]
    public int PageNumber { get; init; } = 1;

    [Range(1, PaginationRequest.MaxPageSize)]
    public int PageSize { get; init; } = 20;

    public Guid? Id { get; init; }
    public string? SourceSystem { get; init; }
    public string? ExternalOrderId { get; init; }
    public string? CustomerName { get; init; }
    public OrderStatus? OrderStatus { get; init; }
    public PaymentStatus? PaymentStatus { get; init; }
    public decimal? TotalAmountFrom { get; init; }
    public decimal? TotalAmountTo { get; init; }
    public string? Currency { get; init; }
    public DateTimeOffset? CreatedAtFrom { get; init; }
    public DateTimeOffset? CreatedAtTo { get; init; }
    public string? SearchTerm { get; init; }
    public OrdersSortBy SortBy { get; init; } = OrdersSortBy.CreatedAt;
    public SortDirection SortDirection { get; init; } = SortDirection.Descending;
}
