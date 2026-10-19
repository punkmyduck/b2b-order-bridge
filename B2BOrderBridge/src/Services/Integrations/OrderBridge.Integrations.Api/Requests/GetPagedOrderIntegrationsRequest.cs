using System.ComponentModel.DataAnnotations;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Integrations.Application.ValueObjects;
using OrderBridge.Integrations.Domain.ValueObjects;
using Shared.Application.Pagination;

namespace OrderBridge.Integrations.Api.Requests;

public sealed class GetPagedOrderIntegrationsRequest
{
    [Range(1, int.MaxValue)]
    public int PageNumber { get; init; } = 1;

    [Range(1, PaginationRequest.MaxPageSize)]
    public int PageSize { get; init; } = 20;

    public Guid? Id { get; init; }
    public Guid? OrderId { get; init; }
    public IntegrationTarget? IntegrationTarget { get; init; }
    public IntegrationOperation? IntegrationOperation { get; init; }
    public IntegrationStatus? IntegrationStatus { get; init; }
    public int? AttemptCountFrom { get; init; }
    public int? AttemptCountTo { get; init; }
    public DateTimeOffset? CreatedAtFrom { get; init; }
    public DateTimeOffset? CreatedAtTo { get; init; }
    public DateTimeOffset? NextAttemptAtTo { get; init; }
    public string? SearchTerm { get; init; }
    public OrderIntegrationsSortBy SortBy { get; init; } = OrderIntegrationsSortBy.CreatedAt;
    public SortDirection SortDirection { get; init; } = SortDirection.Descending;
}
