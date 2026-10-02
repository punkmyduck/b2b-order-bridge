using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OrderBridge.Application.Features.OrderIntegrations.Repositories;
using OrderBridge.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.Models;
using Shared.Application.Pagination;

namespace OrderBridge.Infrastructure.Persistence;

public sealed class OrderIntegrationReadRepository(OrderBridgeDbContext context)
    : IOrderIntegrationReadRepository
{
    private static readonly Expression<Func<OrderIntegration, OrderIntegrationDto>> Projection =
        integration => new OrderIntegrationDto(
            integration.Id,
            integration.OrderId,
            integration.IntegrationTarget,
            integration.IntegrationOperation,
            integration.IntegrationStatus,
            integration.ExternalEntityId,
            integration.IdempotencyKey,
            integration.AttemptCount,
            integration.NextAttemptAt,
            integration.LastErrorCode,
            integration.LastErrorMessage,
            integration.CreatedAt,
            integration.LastAttemptAt,
            integration.CompletedAt,
            integration.UpdatedAt);

    public async Task<PaginationResult<OrderIntegrationDto>> GetPagedAsync(
        PaginationRequest pagination,
        OrderIntegrationsFilter? filter = null,
        OrderIntegrationsSortBy sortBy = OrderIntegrationsSortBy.CreatedAt,
        SortDirection sortDirection = SortDirection.Descending,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        var query = context.OrderIntegrations.AsNoTracking();
        if (filter is not null)
            query = ApplyFilters(query, filter);

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await ApplySorting(query, sortBy, sortDirection)
            .Skip(pagination.Offset)
            .Take(pagination.PageSize)
            .Select(Projection)
            .ToListAsync(cancellationToken);

        return new PaginationResult<OrderIntegrationDto>(items, totalCount, pagination);
    }

    private static IQueryable<OrderIntegration> ApplyFilters(
        IQueryable<OrderIntegration> query,
        OrderIntegrationsFilter filter)
    {
        if (filter.Id.HasValue)
            query = query.Where(integration => integration.Id == filter.Id.Value);
        if (filter.OrderId.HasValue)
            query = query.Where(integration => integration.OrderId == filter.OrderId.Value);
        if (filter.IntegrationTarget.HasValue)
            query = query.Where(integration =>
                integration.IntegrationTarget == filter.IntegrationTarget.Value);
        if (filter.IntegrationOperation.HasValue)
            query = query.Where(integration =>
                integration.IntegrationOperation == filter.IntegrationOperation.Value);
        if (filter.IntegrationStatus.HasValue)
            query = query.Where(integration =>
                integration.IntegrationStatus == filter.IntegrationStatus.Value);
        if (filter.AttemptCountFrom.HasValue)
            query = query.Where(integration =>
                integration.AttemptCount >= filter.AttemptCountFrom.Value);
        if (filter.AttemptCountTo.HasValue)
            query = query.Where(integration =>
                integration.AttemptCount <= filter.AttemptCountTo.Value);
        if (filter.CreatedAtFrom.HasValue)
            query = query.Where(integration =>
                integration.CreatedAt >= filter.CreatedAtFrom.Value);
        if (filter.CreatedAtTo.HasValue)
            query = query.Where(integration =>
                integration.CreatedAt <= filter.CreatedAtTo.Value);
        if (filter.NextAttemptAtTo.HasValue)
            query = query.Where(integration =>
                integration.NextAttemptAt != null &&
                integration.NextAttemptAt <= filter.NextAttemptAtTo.Value);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.Trim();
            var pattern = $"%{searchTerm}%";
            var isIntegrationId = Guid.TryParse(searchTerm, out var integrationId);

            query = query.Where(integration =>
                (isIntegrationId && integration.Id == integrationId) ||
                EF.Functions.ILike(integration.IdempotencyKey, pattern) ||
                (integration.ExternalEntityId != null &&
                 EF.Functions.ILike(integration.ExternalEntityId, pattern)) ||
                (integration.LastErrorCode != null &&
                 EF.Functions.ILike(integration.LastErrorCode, pattern)) ||
                (integration.LastErrorMessage != null &&
                 EF.Functions.ILike(integration.LastErrorMessage, pattern)));
        }

        return query;
    }

    private static IOrderedQueryable<OrderIntegration> ApplySorting(
        IQueryable<OrderIntegration> query,
        OrderIntegrationsSortBy sortBy,
        SortDirection direction)
        => (sortBy, direction) switch
        {
            (OrderIntegrationsSortBy.IntegrationTarget, SortDirection.Ascending) =>
                query.OrderBy(x => x.IntegrationTarget).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.IntegrationTarget, SortDirection.Descending) =>
                query.OrderByDescending(x => x.IntegrationTarget).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.IntegrationOperation, SortDirection.Ascending) =>
                query.OrderBy(x => x.IntegrationOperation).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.IntegrationOperation, SortDirection.Descending) =>
                query.OrderByDescending(x => x.IntegrationOperation).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.IntegrationStatus, SortDirection.Ascending) =>
                query.OrderBy(x => x.IntegrationStatus).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.IntegrationStatus, SortDirection.Descending) =>
                query.OrderByDescending(x => x.IntegrationStatus).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.AttemptCount, SortDirection.Ascending) =>
                query.OrderBy(x => x.AttemptCount).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.AttemptCount, SortDirection.Descending) =>
                query.OrderByDescending(x => x.AttemptCount).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.NextAttemptAt, SortDirection.Ascending) =>
                query.OrderBy(x => x.NextAttemptAt).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.NextAttemptAt, SortDirection.Descending) =>
                query.OrderByDescending(x => x.NextAttemptAt).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.LastAttemptAt, SortDirection.Ascending) =>
                query.OrderBy(x => x.LastAttemptAt).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.LastAttemptAt, SortDirection.Descending) =>
                query.OrderByDescending(x => x.LastAttemptAt).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.CompletedAt, SortDirection.Ascending) =>
                query.OrderBy(x => x.CompletedAt).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.CompletedAt, SortDirection.Descending) =>
                query.OrderByDescending(x => x.CompletedAt).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.UpdatedAt, SortDirection.Ascending) =>
                query.OrderBy(x => x.UpdatedAt).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.UpdatedAt, SortDirection.Descending) =>
                query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.CreatedAt, SortDirection.Ascending) =>
                query.OrderBy(x => x.CreatedAt).ThenBy(x => x.Id),
            (OrderIntegrationsSortBy.CreatedAt, SortDirection.Descending) =>
                query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id),
            _ => throw new ArgumentOutOfRangeException(
                nameof(sortBy), sortBy, "The requested integration sort is not supported.")
        };
}
