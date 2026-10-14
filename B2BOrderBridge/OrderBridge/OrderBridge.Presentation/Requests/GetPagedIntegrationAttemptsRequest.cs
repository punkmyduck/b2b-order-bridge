using System.ComponentModel.DataAnnotations;
using OrderBridge.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Application.ValueObjects;
using Shared.Application.Pagination;

namespace OrderBridge.Presentation.Requests;

public sealed class GetPagedIntegrationAttemptsRequest
{
    [Range(1, int.MaxValue)]
    public int PageNumber { get; init; } = 1;

    [Range(1, PaginationRequest.MaxPageSize)]
    public int PageSize { get; init; } = 20;

    public Guid? Id { get; init; }
    public Guid? OrderIntegrationId { get; init; }
    public int? AttemptNumberFrom { get; init; }
    public int? AttemptNumberTo { get; init; }
    public IntegrationAttemptOutcome? Outcome { get; init; }
    public int? ResponseStatusCode { get; init; }
    public string? ErrorCodeSearchTerm { get; init; }
    public string? ErrorMessageSearchTerm { get; init; }
    public string? TraceIdSearchTerm { get; init; }
    public DateTimeOffset? StartedAtFrom { get; init; }
    public DateTimeOffset? StartedAtTo { get; init; }
    public DateTimeOffset? CompletedAtFrom { get; init; }
    public DateTimeOffset? CompletedAtTo { get; init; }
    public string? SearchTerm { get; init; }
    public IntegrationAttemptSortBy SortBy { get; init; } = IntegrationAttemptSortBy.StartedAt;
    public SortDirection SortDirection { get; init; } = SortDirection.Descending;
}
