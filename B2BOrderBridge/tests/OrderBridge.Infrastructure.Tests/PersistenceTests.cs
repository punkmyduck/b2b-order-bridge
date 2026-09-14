using OrderBridge.Application.ValueObjects;
using OrderBridge.Application.Services.Implementations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OrderBridge.Application.Features.Orders.Commands;
using OrderBridge.Application.Features.Orders.Queries;
using OrderBridge.Application.Features.Orders.Repositories;
using OrderBridge.Domain.Models;
using OrderBridge.Domain.ValueObjects;
using OrderBridge.Infrastructure;
using OrderBridge.Infrastructure.Persistence;
using Shared.Application.Persistence;
using Shared.Application.Services;
using Shared.Application.Results;
using Testcontainers.PostgreSql;
using Xunit;

namespace OrderBridge.Infrastructure.Tests;

public sealed class DatabaseFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgres:17-alpine").Build();
    public ServiceProvider Services { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await _database.StartAsync();
        Services = new ServiceCollection().AddServices().AddPersistence(_database.GetConnectionString())
            .BuildServiceProvider(new ServiceProviderOptions { ValidateScopes = true, ValidateOnBuild = true });
        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<OrderBridgeDbContext>().Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        await Services.DisposeAsync();
        await _database.DisposeAsync();
    }
}

public sealed class PersistenceTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static Order Create(string? externalId = null) => Order.CreateOrder(
        Guid.NewGuid(), "portal", externalId ?? Guid.NewGuid().ToString(),
        new CustomerSnapshot("ACME", "123", "test@example.com"), "RUB",
        new DateTimeOffset(2026, 9, 10, 12, 0, 0, TimeSpan.FromHours(3)),
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
}

