using OrderBridge.Application.Features.OrderIntegrations.Models;
using OrderBridge.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Application.ValueObjects;
using Shared.Application.Pagination;

namespace OrderBridge.Application.Features.OrderIntegrations.Repositories;

public interface IIntegrationAttemptRepository
{
    void Add(IntegrationAttempt attempt);

    Task<IntegrationAttempt?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PaginationResult<IntegrationAttempt>> GetPagedAsync(
        PaginationRequest pagination,
        IntegrationAttemptFilter? filter = null,
        IntegrationAttemptSortBy sortBy = IntegrationAttemptSortBy.StartedAt,
        SortDirection sortDirection = SortDirection.Descending,
        CancellationToken cancellationToken = default);
}
