using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.Persistence;
using Shared.Application.Events;
using Shared.Application.Pagination;
using Shared.Application.Services;
using Shared.Application.Results;
using Xunit;
using Npgsql;
using OrderBridge.Orders.Domain.Models;
using OrderBridge.Orders.Domain.ValueObjects;
using OrderBridge.Orders.Application.ValueObjects;
using OrderBridge.Orders.Application.Features.Orders.Commands;
using OrderBridge.Orders.Application.Features.Orders.Queries;
using OrderBridge.Orders.Application.Features.Orders.ValueObjects;
using OrderBridge.Orders.Application.Features.Orders.Repositories;
using OrderBridge.Orders.Infrastructure.Persistence;

namespace OrderBridge.Orders.Infrastructure.Tests;
public sealed class PersistenceTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static Order Create(
        string? externalId = null,
        string sourceSystem = "portal",
        DateTimeOffset? createdAt = null) => Order.CreateOrder(
        Guid.NewGuid(), sourceSystem, externalId ?? Guid.NewGuid().ToString(),
        new CustomerSnapshot("ACME", "123", "test@example.com"), "RUB",
        createdAt ?? new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.FromHours(3)),
        [new OrderLine(Guid.NewGuid(), "SKU", "Item", 0.333m, 0.10m)]);

    [Fact]
    public async Task Round_trip_loads_complete_aggregate_and_updates_tracked_state()
    {
        var order = Create();
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            Assert.IsType<OrdersUnitOfWork>(work);
            scope.ServiceProvider.GetRequiredService<IRepository<Order, Guid>>().Add(order);
            await work.SaveChangesAsync();
        }
        using (var scope = fixture.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IRepository<Order, Guid>>();
            Assert.True(await repo.ExistsAsync(order.Id));
            var loaded = (await repo.GetByIdAsync(order.Id))!;
            Assert.Equal(order.Customer, loaded.Customer);
            Assert.Equal(0.333m, Assert.Single(loaded.Lines).Quantity);
            Assert.Equal(0.03m, loaded.TotalAmount);
            Assert.Equal(order.CreatedAt, loaded.CreatedAt);
            Assert.Empty(loaded.DomainEvents);
            loaded.Cancel(DateTimeOffset.UtcNow);
            repo.Update(loaded);
            await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }
        using var readScope = fixture.Services.CreateScope();
        var saved = await readScope.ServiceProvider.GetRequiredService<IReadRepository<Order, Guid>>()
            .GetByIdAsync(order.Id);
        Assert.Equal(OrderStatus.Cancelled, saved!.OrderStatus);
    }

    [Fact]
    public async Task Duplicate_order_rolls_back_entire_save()
    {
        var externalId = Guid.NewGuid().ToString();
        using var scope = fixture.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        db.Orders.AddRange(Create(externalId), Create(externalId));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)exception.InnerException!).SqlState);
        db.ChangeTracker.Clear();
        Assert.False(await db.Orders.AnyAsync(x => x.ExternalOrderId == externalId));
    }

    [Fact]
    public async Task Existing_command_saves_using_registered_services()
    {
        Guid id;
        using (var scope = fixture.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var handler = new CreateOrderCommandHandler(services.GetRequiredService<IRepository<Order, Guid>>(),
                services.GetRequiredService<IDomainEventDispatcher>(),
                services.GetRequiredService<IUnitOfWork>(), services.GetRequiredService<IDateTimeOffsetProvider>());
            var result = await handler.Handle(new CreateOrderCommand("portal", Guid.NewGuid().ToString(),
                new CustomerSnapshotDto("ACME", "123", "test@example.com"), "RUB",
                [new OrderLineDto("SKU", "Item", 1m, 10m)]), CancellationToken.None);
            id = result.Value.Id;
        }
        using var read = fixture.Services.CreateScope();
        Assert.True(await read.ServiceProvider.GetRequiredService<IReadRepository<Order, Guid>>().ExistsAsync(id));
        var db = read.ServiceProvider.GetRequiredService<OrdersDbContext>();
        Assert.Single(await db.OutboxMessages.Where(x => x.OrderingKey == id.ToString("N")).ToListAsync());
    }

    [Fact]
    public async Task Get_by_id_projects_summary_without_tracking_and_reports_not_found()
    {
        var order = Create();
        using (var writeScope = fixture.Services.CreateScope())
        {
            writeScope.ServiceProvider.GetRequiredService<IRepository<Order, Guid>>().Add(order);
            await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        using var readScope = fixture.Services.CreateScope();
        var services = readScope.ServiceProvider;
        var handler = new GetOrderByIdQueryHandler(services.GetRequiredService<IOrderReadRepository>());

        var found = await handler.Handle(new GetOrderByIdQuery(order.Id), CancellationToken.None);

        Assert.True(found.IsSuccess);
        Assert.Equal(order.Id, found.Value.Id);
        Assert.Equal(order.Customer.CompanyName, found.Value.Customer.CompanyName);
        Assert.Empty(services.GetRequiredService<OrdersDbContext>().ChangeTracker.Entries());

        var missing = await handler.Handle(new GetOrderByIdQuery(Guid.NewGuid()), CancellationToken.None);

        Assert.True(missing.IsFailure);
        Assert.Equal(ErrorType.NotFound, missing.Error!.Type);
        Assert.Equal("Order.NotFound", missing.Error.Code);
    }

    [Fact]
    public async Task Get_paged_returns_stably_sorted_page_and_total_count()
    {
        var sourceSystem = $"paged-{Guid.NewGuid():N}";
        using (var writeScope = fixture.Services.CreateScope())
        {
            var writeRepository = writeScope.ServiceProvider.GetRequiredService<IRepository<Order, Guid>>();
            writeRepository.Add(Create("order-c", sourceSystem));
            writeRepository.Add(Create("order-a", sourceSystem));
            writeRepository.Add(Create("order-b", sourceSystem));
            await writeScope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        using var readScope = fixture.Services.CreateScope();
        var readRepository = readScope.ServiceProvider.GetRequiredService<IOrderReadRepository>();

        var page = await readRepository.GetPagedAsync(
            new PaginationRequest(pageNumber: 1, pageSize: 2),
            new OrdersFilter(SourceSystem: sourceSystem),
            OrdersSortBy.ExternalOrderId,
            SortDirection.Ascending);

        Assert.Equal(3, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.True(page.HasNextPage);
        Assert.Equal(["order-a", "order-b"], page.Items.Select(order => order.ExternalOrderId));
        Assert.Empty(readScope.ServiceProvider.GetRequiredService<OrdersDbContext>().ChangeTracker.Entries());
    }

    [Fact]
    public async Task Get_paged_returns_successful_empty_page_when_nothing_matches()
    {
        using var scope = fixture.Services.CreateScope();
        var handler = new GetPagedOrdersQueryHandler(
            scope.ServiceProvider.GetRequiredService<IOrderReadRepository>());

        var result = await handler.Handle(
            new GetPagedOrdersQuery(
                new PaginationRequest(),
                new OrdersFilter(SourceSystem: $"missing-{Guid.NewGuid():N}")),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Empty(result.Value.Items);
        Assert.Equal(0, result.Value.TotalCount);
        Assert.Equal(0, result.Value.TotalPages);
    }

    [Fact]
    public async Task Get_details_returns_order_lines_without_tracking()
    {
        var order = Create();
        using (var writeScope = fixture.Services.CreateScope())
        {
            var dbContext = writeScope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            dbContext.Add(order);
            await dbContext.SaveChangesAsync();
        }

        using var readScope = fixture.Services.CreateScope();
        var handler = new GetOrderDetailsByIdQueryHandler(
            readScope.ServiceProvider.GetRequiredService<IOrderReadRepository>());

        var result = await handler.Handle(
            new GetOrderDetailsByIdQuery(order.Id),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Single(result.Value.Lines);
        Assert.Equal(order.TotalAmount, result.Value.Lines.Single().LineTotal);
        Assert.Empty(readScope.ServiceProvider
            .GetRequiredService<OrdersDbContext>().ChangeTracker.Entries());
    }

}
