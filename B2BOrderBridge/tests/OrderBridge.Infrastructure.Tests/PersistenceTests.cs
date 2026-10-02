using OrderBridge.Application.Features.OrderIntegrations.Services.Implementations;
using OrderBridge.Application.Features.OrderIntegrations.Repositories;
using OrderBridge.Application.Features.OrderIntegrations.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OrderBridge.Application.Features.Orders.Commands;
using OrderBridge.Application.Features.Orders.Queries;
using OrderBridge.Application.Features.Orders.Repositories;
using OrderBridge.Application.Features.Orders.ValueObjects;
using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.Models;
using OrderBridge.Domain.ValueObjects;
using OrderBridge.Infrastructure;
using OrderBridge.Infrastructure.Persistence;
using Shared.Application.Persistence;
using Shared.Application.Pagination;
using Shared.Application.Services;
using Shared.Application.Results;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderBridge.Infrastructure.Tests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    private ServiceProvider? _services;
    public ServiceProvider Services => _services
        ?? throw new InvalidOperationException("The database fixture has not been initialized.");

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        _services = new ServiceCollection()
            .AddLogging()
            .AddServices()
            .AddPersistence(_database.GetConnectionString())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OrderBridgeDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_services is not null)
            await _services.DisposeAsync();
        await _database.DisposeAsync();
    }
}

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
            var db = scope.ServiceProvider.GetRequiredService<OrderBridgeDbContext>();
            var work = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            Assert.Same(db, work);
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
        var db = scope.ServiceProvider.GetRequiredService<OrderBridgeDbContext>();
        db.Orders.AddRange(Create(externalId), Create(externalId));
        var exception = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal(PostgresErrorCodes.UniqueViolation, ((PostgresException)exception.InnerException!).SqlState);
        db.ChangeTracker.Clear();
        Assert.False(await db.Orders.AnyAsync(x => x.ExternalOrderId == externalId));
    }

    [Fact]
    public async Task Concurrent_integration_update_is_rejected()
    {
        var order = Create();
        var task = OrderIntegration.Create(Guid.NewGuid(), order, IntegrationTarget.OneC,
            IntegrationOperation.CreateSalesOrder, Guid.NewGuid().ToString(), DateTimeOffset.UtcNow, false);
        using (var scope = fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrderBridgeDbContext>();
            db.AddRange(order, task);
            await db.SaveChangesAsync();
        }
        using var first = fixture.Services.CreateScope();
        using var second = fixture.Services.CreateScope();
        var a = first.ServiceProvider.GetRequiredService<OrderBridgeDbContext>();
        var b = second.ServiceProvider.GetRequiredService<OrderBridgeDbContext>();
        var firstTask = await a.OrderIntegrations.SingleAsync(x => x.Id == task.Id);
        var secondTask = await b.OrderIntegrations.SingleAsync(x => x.Id == task.Id);
        firstTask.StartAttempt(DateTimeOffset.UtcNow);
        secondTask.StartAttempt(DateTimeOffset.UtcNow);
        await a.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => b.SaveChangesAsync());
    }

    [Fact]
    public async Task Existing_command_saves_using_registered_services()
    {
        Guid id;
        using (var scope = fixture.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var handler = new CreateOrderCommandHandler(services.GetRequiredService<IRepository<Order, Guid>>(),
                services.GetRequiredService<IRepository<OrderIntegration, Guid>>(), services.GetRequiredService<IUnitOfWork>(), services.GetRequiredService<IDateTimeOffsetProvider>(), new OrderIntegrationPlanner());
            var result = await handler.Handle(new CreateOrderCommand("portal", Guid.NewGuid().ToString(),
                new CustomerSnapshotDto("ACME", "123", "test@example.com"), "RUB",
                [new OrderLineDto("SKU", "Item", 1m, 10m)]), CancellationToken.None);
            id = result.Value.Id;
        }
        using var read = fixture.Services.CreateScope();
        Assert.True(await read.ServiceProvider.GetRequiredService<IReadRepository<Order, Guid>>().ExistsAsync(id));
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
        Assert.Empty(services.GetRequiredService<OrderBridgeDbContext>().ChangeTracker.Entries());

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
        Assert.Empty(readScope.ServiceProvider.GetRequiredService<OrderBridgeDbContext>().ChangeTracker.Entries());
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
    public async Task Get_details_returns_lines_and_integrations_without_tracking()
    {
        var order = Create();
        var createdAt = DateTimeOffset.UtcNow;
        var integration = OrderIntegration.Create(
            Guid.NewGuid(),
            order,
            IntegrationTarget.OneC,
            IntegrationOperation.CreateSalesOrder,
            Guid.NewGuid().ToString("N"),
            createdAt,
            isWaitingForPrerequisite: false);

        using (var writeScope = fixture.Services.CreateScope())
        {
            var dbContext = writeScope.ServiceProvider.GetRequiredService<OrderBridgeDbContext>();
            dbContext.AddRange(order, integration);
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
        Assert.Equal(integration.Id, Assert.Single(result.Value.Integrations).Id);
        Assert.Empty(readScope.ServiceProvider
            .GetRequiredService<OrderBridgeDbContext>().ChangeTracker.Entries());
    }

    [Fact]
    public async Task Get_paged_integrations_filters_by_order_and_reports_total_count()
    {
        var order = Create();
        var createdAt = DateTimeOffset.UtcNow;
        var first = OrderIntegration.Create(
            Guid.NewGuid(), order, IntegrationTarget.OneC,
            IntegrationOperation.CreateSalesOrder, Guid.NewGuid().ToString("N"),
            createdAt, isWaitingForPrerequisite: false);
        var second = OrderIntegration.Create(
            Guid.NewGuid(), order, IntegrationTarget.Diadoc,
            IntegrationOperation.CreateDocument, Guid.NewGuid().ToString("N"),
            createdAt, isWaitingForPrerequisite: true);

        using (var writeScope = fixture.Services.CreateScope())
        {
            var dbContext = writeScope.ServiceProvider.GetRequiredService<OrderBridgeDbContext>();
            dbContext.AddRange(order, first, second);
            await dbContext.SaveChangesAsync();
        }

        using var readScope = fixture.Services.CreateScope();
        var repository = readScope.ServiceProvider
            .GetRequiredService<IOrderIntegrationReadRepository>();

        var page = await repository.GetPagedAsync(
            new PaginationRequest(pageNumber: 1, pageSize: 1),
            new OrderIntegrationsFilter(OrderId: order.Id),
            OrderIntegrationsSortBy.CreatedAt,
            SortDirection.Ascending);

        Assert.Equal(2, page.TotalCount);
        Assert.Equal(2, page.TotalPages);
        Assert.Single(page.Items);
        Assert.Equal(order.Id, page.Items[0].OrderId);
        Assert.True(page.HasNextPage);
    }
}

