using OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Integrations.Application.ValueObjects;
using Shared.Application.Pagination;

namespace OrderBridge.Integrations.Application.Features.OrderIntegrations.Repositories;

public interface IOrderIntegrationReadRepository
{
    Task<PaginationResult<OrderIntegrationDto>> GetPagedAsync(
        PaginationRequest pagination,
        OrderIntegrationsFilter? filter = null,
        OrderIntegrationsSortBy sortBy = OrderIntegrationsSortBy.CreatedAt,
        SortDirection sortDirection = SortDirection.Descending,
        CancellationToken cancellationToken = default);
}
