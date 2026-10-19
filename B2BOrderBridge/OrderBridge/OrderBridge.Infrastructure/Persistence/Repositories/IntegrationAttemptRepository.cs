using Microsoft.EntityFrameworkCore;
using OrderBridge.Application.Features.OrderIntegrations.Models;
using OrderBridge.Application.Features.OrderIntegrations.Repositories;
using OrderBridge.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Application.ValueObjects;
using Shared.Application.Pagination;

namespace OrderBridge.Infrastructure.Persistence.Repositories;

public sealed class IntegrationAttemptRepository(OrderBridgeDbContext context)
    : IIntegrationAttemptRepository
{
    public void Add(IntegrationAttempt attempt)
    {
        ArgumentNullException.ThrowIfNull(attempt);
        context.IntegrationAttempts.Add(attempt);
    }

    public async Task<IntegrationAttempt?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => await context.IntegrationAttempts.FindAsync([id], cancellationToken);

    public async Task<PaginationResult<IntegrationAttempt>> GetPagedAsync(
        PaginationRequest pagination,
        IntegrationAttemptFilter? filter = null,
        IntegrationAttemptSortBy sortBy = IntegrationAttemptSortBy.StartedAt,
        SortDirection sortDirection = SortDirection.Descending,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pagination);

        var query = context.IntegrationAttempts.AsNoTracking();
        if (filter is not null)
            query = ApplyFilters(query, filter);

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await ApplySorting(query, sortBy, sortDirection)
            .Skip(pagination.Offset)
            .Take(pagination.PageSize)
            .ToListAsync(cancellationToken);

        return new PaginationResult<IntegrationAttempt>(items, totalCount, pagination);
    }

    private static IQueryable<IntegrationAttempt> ApplyFilters(
        IQueryable<IntegrationAttempt> query,
        IntegrationAttemptFilter filter)
    {
        if (filter.Id.HasValue)
            query = query.Where(attempt => attempt.Id == filter.Id.Value);
        if (filter.OrderIntegrationId.HasValue)
            query = query.Where(attempt =>
                attempt.OrderIntegrationId == filter.OrderIntegrationId.Value);
        if (filter.AttemptNumberFrom.HasValue)
            query = query.Where(attempt =>
                attempt.AttemptNumber >= filter.AttemptNumberFrom.Value);
        if (filter.AttemptNumberTo.HasValue)
            query = query.Where(attempt =>
                attempt.AttemptNumber <= filter.AttemptNumberTo.Value);
        if (filter.Outcome.HasValue)
            query = query.Where(attempt => attempt.Outcome == filter.Outcome.Value);
        if (filter.ResponseStatusCode.HasValue)
            query = query.Where(attempt =>
                attempt.ResponseStatusCode == filter.ResponseStatusCode.Value);
        if (filter.StartedAtFrom.HasValue)
            query = query.Where(attempt => attempt.StartedAt >= filter.StartedAtFrom.Value);
        if (filter.StartedAtTo.HasValue)
            query = query.Where(attempt => attempt.StartedAt <= filter.StartedAtTo.Value);
        if (filter.CompletedAtFrom.HasValue)
            query = query.Where(attempt =>
                attempt.CompletedAt != null &&
                attempt.CompletedAt >= filter.CompletedAtFrom.Value);
        if (filter.CompletedAtTo.HasValue)
            query = query.Where(attempt =>
                attempt.CompletedAt != null &&
                attempt.CompletedAt <= filter.CompletedAtTo.Value);

        if (!string.IsNullOrWhiteSpace(filter.ErrorCodeSearchTerm))
        {
            var pattern = $"%{filter.ErrorCodeSearchTerm.Trim()}%";
            query = query.Where(attempt =>
                attempt.ErrorCode != null &&
                EF.Functions.ILike(attempt.ErrorCode, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.ErrorMessageSearchTerm))
        {
            var pattern = $"%{filter.ErrorMessageSearchTerm.Trim()}%";
            query = query.Where(attempt =>
                attempt.ErrorMessage != null &&
                EF.Functions.ILike(attempt.ErrorMessage, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.TraceIdSearchTerm))
        {
            var pattern = $"%{filter.TraceIdSearchTerm.Trim()}%";
            query = query.Where(attempt =>
                attempt.TraceId != null &&
                EF.Functions.ILike(attempt.TraceId, pattern));
        }

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.Trim();
            var pattern = $"%{searchTerm}%";
            var isId = Guid.TryParse(searchTerm, out var id);
            var isNumber = int.TryParse(searchTerm, out var number);
            var isOutcome = Enum.TryParse<IntegrationAttemptOutcome>(
                searchTerm,
                ignoreCase: true,
                out var outcome);

            query = query.Where(attempt =>
                (isId && (attempt.Id == id || attempt.OrderIntegrationId == id)) ||
                (isNumber && (attempt.AttemptNumber == number ||
                              attempt.ResponseStatusCode == number)) ||
                (isOutcome && attempt.Outcome == outcome) ||
                (attempt.ErrorCode != null &&
                 EF.Functions.ILike(attempt.ErrorCode, pattern)) ||
                (attempt.ErrorMessage != null &&
                 EF.Functions.ILike(attempt.ErrorMessage, pattern)) ||
                (attempt.TraceId != null &&
                 EF.Functions.ILike(attempt.TraceId, pattern)));
        }

        return query;
    }

    private static IOrderedQueryable<IntegrationAttempt> ApplySorting(
        IQueryable<IntegrationAttempt> query,
        IntegrationAttemptSortBy sortBy,
        SortDirection direction)
        => (sortBy, direction) switch
        {
            (IntegrationAttemptSortBy.Id, SortDirection.Ascending) =>
                query.OrderBy(x => x.Id),
            (IntegrationAttemptSortBy.Id, SortDirection.Descending) =>
                query.OrderByDescending(x => x.Id),
            (IntegrationAttemptSortBy.OrderIntegrationId, SortDirection.Ascending) =>
                query.OrderBy(x => x.OrderIntegrationId).ThenBy(x => x.Id),
            (IntegrationAttemptSortBy.OrderIntegrationId, SortDirection.Descending) =>
                query.OrderByDescending(x => x.OrderIntegrationId).ThenBy(x => x.Id),
            (IntegrationAttemptSortBy.AttemptNumber, SortDirection.Ascending) =>
                query.OrderBy(x => x.AttemptNumber).ThenBy(x => x.Id),
            (IntegrationAttemptSortBy.AttemptNumber, SortDirection.Descending) =>
                query.OrderByDescending(x => x.AttemptNumber).ThenBy(x => x.Id),
            (IntegrationAttemptSortBy.Outcome, SortDirection.Ascending) =>
                query.OrderBy(x => x.Outcome).ThenBy(x => x.Id),
            (IntegrationAttemptSortBy.Outcome, SortDirection.Descending) =>
                query.OrderByDescending(x => x.Outcome).ThenBy(x => x.Id),
            (IntegrationAttemptSortBy.ResponseStatusCode, SortDirection.Ascending) =>
                query.OrderBy(x => x.ResponseStatusCode).ThenBy(x => x.Id),
            (IntegrationAttemptSortBy.ResponseStatusCode, SortDirection.Descending) =>
                query.OrderByDescending(x => x.ResponseStatusCode).ThenBy(x => x.Id),
            (IntegrationAttemptSortBy.StartedAt, SortDirection.Ascending) =>
                query.OrderBy(x => x.StartedAt).ThenBy(x => x.Id),
            (IntegrationAttemptSortBy.StartedAt, SortDirection.Descending) =>
                query.OrderByDescending(x => x.StartedAt).ThenBy(x => x.Id),
            (IntegrationAttemptSortBy.CompletedAt, SortDirection.Ascending) =>
                query.OrderBy(x => x.CompletedAt).ThenBy(x => x.Id),
            (IntegrationAttemptSortBy.CompletedAt, SortDirection.Descending) =>
                query.OrderByDescending(x => x.CompletedAt).ThenBy(x => x.Id),
            _ => throw new ArgumentOutOfRangeException(
                nameof(sortBy), sortBy, "The requested integration attempt sort is not supported.")
        };
}
