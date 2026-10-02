using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using OrderBridge.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Application.Features.Orders.Repositories;
using OrderBridge.Application.Features.Orders.ValueObjects;
using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.Models;
using Shared.Application.Pagination;

namespace OrderBridge.Infrastructure.Persistence;

public sealed class OrderReadRepository(OrderBridgeDbContext context)
    : EfRepository<Order, Guid>(context), IOrderReadRepository
{
    private static readonly Expression<Func<Order, OrderSummaryDto>> SummaryProjection =
        order => new OrderSummaryDto(
            order.Id,
            order.SourceSystem,
            order.ExternalOrderId,
            new CustomerSnapshotDto(
                order.Customer.CompanyName,
                order.Customer.TaxId,
                order.Customer.ContactEmail),
            order.OrderStatus,
            order.PaymentStatus,
            order.TotalAmount,
            order.Currency,
            order.CreatedAt);

    public async Task<PaginationResult<OrderSummaryDto>> GetPagedAsync(
        PaginationRequest paginationRequest,
        OrdersFilter? filter = null,
        OrdersSortBy sortBy = OrdersSortBy.CreatedAt,
        SortDirection sortDirection = SortDirection.Descending,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(paginationRequest);

        var query = Context.Orders
            .AsNoTracking()
            .IgnoreAutoIncludes();

        if (filter is not null)
            query = ApplyFilters(query, filter);

        var totalCount = await query.LongCountAsync(cancellationToken);
        var items = await ApplySorting(query, sortBy, sortDirection)
            .Skip(paginationRequest.Offset)
            .Take(paginationRequest.PageSize)
            .Select(SummaryProjection)
            .ToListAsync(cancellationToken);

        return new PaginationResult<OrderSummaryDto>(items, totalCount, paginationRequest);
    }

    public Task<OrderSummaryDto?> GetSummaryByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
        => Context.Orders
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Where(order => order.Id == id)
            .Select(SummaryProjection)
            .SingleOrDefaultAsync(cancellationToken);

    public async Task<OrderDetailsDto?> GetDetailsByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var order = await Context.Orders
            .AsNoTracking()
            .IgnoreAutoIncludes()
            .Where(order => order.Id == id)
            .Select(order => new OrderDetailsDto(
                order.Id,
                order.SourceSystem,
                order.ExternalOrderId,
                new CustomerSnapshotDto(
                    order.Customer.CompanyName,
                    order.Customer.TaxId,
                    order.Customer.ContactEmail),
                order.OrderStatus,
                order.PaymentStatus,
                order.TotalAmount,
                order.Currency,
                order.CreatedAt,
                order.UpdatedAt,
                order.Lines
                    .OrderBy(line => line.Id)
                    .Select(line => new OrderLineDetailsDto(
                        line.Id,
                        line.Sku,
                        line.Name,
                        line.Quantity,
                        line.UnitPrice,
                        line.LineTotal))
                    .ToList(),
                Array.Empty<OrderIntegrationDto>()))
            .SingleOrDefaultAsync(cancellationToken);

        if (order is null)
            return null;

        var integrations = await Context.OrderIntegrations
            .AsNoTracking()
            .Where(integration => integration.OrderId == id)
            .OrderBy(integration => integration.CreatedAt)
            .ThenBy(integration => integration.Id)
            .Select(integration => new OrderIntegrationDto(
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
                integration.UpdatedAt))
            .ToListAsync(cancellationToken);

        return order with { Integrations = integrations };
    }

    private static IQueryable<Order> ApplyFilters(
        IQueryable<Order> query,
        OrdersFilter filter)
    {
        if (filter.Id is not null)
            query = query.Where(order => order.Id == filter.Id);

        if (!string.IsNullOrWhiteSpace(filter.SourceSystem))
        {
            var sourceSystem = filter.SourceSystem.Trim();
            query = query.Where(order => order.SourceSystem == sourceSystem);
        }

        if (!string.IsNullOrWhiteSpace(filter.ExternalOrderId))
        {
            var externalOrderId = filter.ExternalOrderId.Trim();
            query = query.Where(order => order.ExternalOrderId == externalOrderId);
        }

        if (!string.IsNullOrWhiteSpace(filter.CustomerName))
        {
            var customerName = filter.CustomerName.Trim();
            query = query.Where(order => order.Customer.CompanyName == customerName);
        }

        if (filter.OrderStatus is not null)
            query = query.Where(order => order.OrderStatus == filter.OrderStatus);

        if (filter.PaymentStatus is not null)
            query = query.Where(order => order.PaymentStatus == filter.PaymentStatus);

        if (filter.TotalAmountFrom is not null)
            query = query.Where(order => order.TotalAmount >= filter.TotalAmountFrom);

        if (filter.TotalAmountTo is not null)
            query = query.Where(order => order.TotalAmount <= filter.TotalAmountTo);

        if (!string.IsNullOrWhiteSpace(filter.Currency))
        {
            var currency = filter.Currency.Trim().ToUpperInvariant();
            query = query.Where(order => order.Currency == currency);
        }

        if (filter.CreatedAtFrom is not null)
            query = query.Where(order => order.CreatedAt >= filter.CreatedAtFrom);

        if (filter.CreatedAtTo is not null)
            query = query.Where(order => order.CreatedAt <= filter.CreatedAtTo);

        if (!string.IsNullOrWhiteSpace(filter.SearchTerm))
        {
            var searchTerm = filter.SearchTerm.Trim();
            var pattern = $"%{searchTerm}%";
            var isOrderId = Guid.TryParse(searchTerm, out var orderId);

            query = query.Where(order =>
                isOrderId && order.Id == orderId ||
                EF.Functions.ILike(order.SourceSystem, pattern) ||
                EF.Functions.ILike(order.ExternalOrderId, pattern) ||
                EF.Functions.ILike(order.Customer.CompanyName, pattern) ||
                EF.Functions.ILike(order.Currency, pattern));
        }

        return query;
    }

    private static IOrderedQueryable<Order> ApplySorting(
        IQueryable<Order> query,
        OrdersSortBy sortBy,
        SortDirection sortDirection)
        => (sortBy, sortDirection) switch
        {
            (OrdersSortBy.SourceSystem, SortDirection.Ascending) =>
                query.OrderBy(order => order.SourceSystem).ThenBy(order => order.Id),
            (OrdersSortBy.SourceSystem, SortDirection.Descending) =>
                query.OrderByDescending(order => order.SourceSystem).ThenBy(order => order.Id),
            (OrdersSortBy.ExternalOrderId, SortDirection.Ascending) =>
                query.OrderBy(order => order.ExternalOrderId).ThenBy(order => order.Id),
            (OrdersSortBy.ExternalOrderId, SortDirection.Descending) =>
                query.OrderByDescending(order => order.ExternalOrderId).ThenBy(order => order.Id),
            (OrdersSortBy.CustomerName, SortDirection.Ascending) =>
                query.OrderBy(order => order.Customer.CompanyName).ThenBy(order => order.Id),
            (OrdersSortBy.CustomerName, SortDirection.Descending) =>
                query.OrderByDescending(order => order.Customer.CompanyName).ThenBy(order => order.Id),
            (OrdersSortBy.OrderStatus, SortDirection.Ascending) =>
                query.OrderBy(order => order.OrderStatus).ThenBy(order => order.Id),
            (OrdersSortBy.OrderStatus, SortDirection.Descending) =>
                query.OrderByDescending(order => order.OrderStatus).ThenBy(order => order.Id),
            (OrdersSortBy.PaymentStatus, SortDirection.Ascending) =>
                query.OrderBy(order => order.PaymentStatus).ThenBy(order => order.Id),
            (OrdersSortBy.PaymentStatus, SortDirection.Descending) =>
                query.OrderByDescending(order => order.PaymentStatus).ThenBy(order => order.Id),
            (OrdersSortBy.TotalAmount, SortDirection.Ascending) =>
                query.OrderBy(order => order.TotalAmount).ThenBy(order => order.Id),
            (OrdersSortBy.TotalAmount, SortDirection.Descending) =>
                query.OrderByDescending(order => order.TotalAmount).ThenBy(order => order.Id),
            (OrdersSortBy.Currency, SortDirection.Ascending) =>
                query.OrderBy(order => order.Currency).ThenBy(order => order.Id),
            (OrdersSortBy.Currency, SortDirection.Descending) =>
                query.OrderByDescending(order => order.Currency).ThenBy(order => order.Id),
            (OrdersSortBy.CreatedAt, SortDirection.Ascending) =>
                query.OrderBy(order => order.CreatedAt).ThenBy(order => order.Id),
            (OrdersSortBy.CreatedAt, SortDirection.Descending) =>
                query.OrderByDescending(order => order.CreatedAt).ThenBy(order => order.Id),
            _ => throw new ArgumentOutOfRangeException(
                nameof(sortBy),
                sortBy,
                "The requested order sort is not supported.")
        };
}
