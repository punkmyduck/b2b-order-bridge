using OrderBridge.Application.Features.OrderIntegrations.Repositories;
using OrderBridge.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Application.ValueObjects;
using Shared.Application.Messaging;
using Shared.Application.Pagination;
using Shared.Application.Results;

namespace OrderBridge.Application.Features.OrderIntegrations.Queries;

public sealed record GetPagedOrderIntegrationsQuery(
    PaginationRequest Pagination,
    OrderIntegrationsFilter? Filter = null,
    OrderIntegrationsSortBy SortBy = OrderIntegrationsSortBy.CreatedAt,
    SortDirection SortDirection = SortDirection.Descending)
    : IQuery<PaginationResult<OrderIntegrationDto>>;

public sealed class GetPagedOrderIntegrationsQueryHandler(
    IOrderIntegrationReadRepository repository)
    : IQueryHandler<GetPagedOrderIntegrationsQuery, PaginationResult<OrderIntegrationDto>>
{
    public async Task<Result<PaginationResult<OrderIntegrationDto>>> Handle(
        GetPagedOrderIntegrationsQuery query,
        CancellationToken cancellationToken)
        => await repository.GetPagedAsync(
            query.Pagination,
            query.Filter,
            query.SortBy,
            query.SortDirection,
            cancellationToken);
}
