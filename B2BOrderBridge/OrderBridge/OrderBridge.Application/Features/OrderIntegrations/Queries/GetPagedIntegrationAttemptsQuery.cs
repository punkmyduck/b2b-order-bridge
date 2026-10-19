using OrderBridge.Application.Features.OrderIntegrations.Models;
using OrderBridge.Application.Features.OrderIntegrations.Repositories;
using OrderBridge.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Application.ValueObjects;
using Shared.Application.Messaging;
using Shared.Application.Pagination;
using Shared.Application.Results;

namespace OrderBridge.Application.Features.OrderIntegrations.Queries;

public sealed record GetPagedIntegrationAttemptsQuery(
    PaginationRequest Pagination,
    IntegrationAttemptFilter? Filter = null,
    IntegrationAttemptSortBy SortBy = IntegrationAttemptSortBy.StartedAt,
    SortDirection SortDirection = SortDirection.Descending)
    : IQuery<PaginationResult<IntegrationAttempt>>;

public sealed class GetPagedIntegrationAttemptsQueryHandler(
    IIntegrationAttemptRepository repository)
    : IQueryHandler<GetPagedIntegrationAttemptsQuery, PaginationResult<IntegrationAttempt>>
{
    public async Task<Result<PaginationResult<IntegrationAttempt>>> Handle(
        GetPagedIntegrationAttemptsQuery request,
        CancellationToken cancellationToken)
        => await repository.GetPagedAsync(
            request.Pagination,
            request.Filter,
            request.SortBy,
            request.SortDirection,
            cancellationToken);
}
