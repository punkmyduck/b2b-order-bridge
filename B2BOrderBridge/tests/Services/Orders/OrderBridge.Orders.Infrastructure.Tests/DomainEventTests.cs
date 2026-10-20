using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OrderBridge.Contracts.Events;
using OrderBridge.Orders.Application.Features.Orders.Commands;
using OrderBridge.Orders.Domain.Events;
using OrderBridge.Orders.Domain.Models;
using OrderBridge.Orders.Infrastructure.Persistence;
using Shared.Application.Events;
using Shared.Application.Persistence;
using Shared.Application.Services;
using Shared.Domain;
using Shared.Messaging.Kafka;
using Xunit;

namespace OrderBridge.Orders.Infrastructure.Tests;

public sealed class DomainEventTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Command_explicitly_dispatches_events_commits_outbox_and_then_clears_events()
    {
        using var scope = fixture.Services.CreateScope();
        var services = scope.ServiceProvider;
        var db = services.GetRequiredService<OrdersDbContext>();
        var dispatcher = new RecordingDispatcher(services.GetRequiredService<IDomainEventDispatcher>());
        var command = Command();
        var result = await Handler(services, dispatcher).Handle(command, default);
        var order = Assert.Single(db.Orders.Local, x => x.Id == result.Value.Id);
        var created = Assert.IsType<OrderCreatedDomainEvent>(Assert.Single(dispatcher.Handled));

        Assert.Empty(order.DomainEvents);
        var saved = await db.OutboxMessages.SingleAsync(x => x.Id == created.EventId);
        var message = EventJson.Deserialize(saved.Payload, OrderCreatedEvent.Definition);

        Assert.Equal(created.EventId, message.Metadata.EventId);
        Assert.Equal(created.OccurredAt, message.Metadata.OccurredAt);
        Assert.Equal(command.ExternalOrderId, message.Payload.Order.ExternalOrderId);
        Assert.Equal(order.Id, message.Payload.Order.Id);
        Assert.Equal(created.Lines[0].Id, message.Payload.Order.Lines[0].Id);
        Assert.Equal("FullCycle", message.Payload.ProcessingScenario);
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        Assert.Single(dispatcher.Handled);
        Assert.Single(await db.OutboxMessages.Where(x => x.OrderingKey == order.Id.ToString("N"))
                .ToListAsync());
        db.ChangeTracker.Clear();
        var reloaded = await services.GetRequiredService<IRepository<Order, Guid>>().GetByIdAsync(order.Id);

        Assert.Empty(reloaded!.DomainEvents);
    }

    [Fact]
    public async Task Unit_of_work_only_saves_without_dispatching_or_clearing_domain_events()
    {
        var order = Create();
        var created = Assert.Single(order.DomainEvents);
        using var scope = fixture.Services.CreateScope();
        var services = scope.ServiceProvider;

        services.GetRequiredService<IRepository<Order, Guid>>().Add(order);
        await services.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        Assert.Same(created, Assert.Single(order.DomainEvents));
        var db = services.GetRequiredService<OrdersDbContext>();

        Assert.True(await db.Orders.AnyAsync(x => x.Id == order.Id));
        Assert.False(await db.OutboxMessages.AnyAsync(x => x.OrderingKey == order.Id.ToString("N")));
    }

    [Fact]
    public async Task Failed_command_save_keeps_events_and_rolls_back_order_and_outbox_together()
    {
        var externalId = Guid.NewGuid().ToString("N");

        using (var seed = fixture.Services.CreateScope())
        {
            seed.ServiceProvider.GetRequiredService<IRepository<Order, Guid>>().Add(Create(externalId));
            await seed.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync();
        }

        Guid orderId;
        Guid eventId;

        using (var scope = fixture.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var dispatcher = new RecordingDispatcher(services.GetRequiredService<IDomainEventDispatcher>());

            await Assert.ThrowsAsync<DbUpdateException>(() => Handler(services, dispatcher).Handle(Command(externalId), default));
            var db = services.GetRequiredService<OrdersDbContext>();
            var order = Assert.Single(db.Orders.Local);
            var created = Assert.Single(order.DomainEvents);

            orderId = order.Id;
            eventId = created.EventId;
            Assert.Same(created, Assert.Single(dispatcher.Handled));
            Assert.Single(db.OutboxMessages.Local, x => x.Id == eventId);
        }

        using var read = fixture.Services.CreateScope();
        var saved = read.ServiceProvider.GetRequiredService<OrdersDbContext>();

        Assert.False(await saved.Orders.AnyAsync(x => x.Id == orderId));
        Assert.False(await saved.OutboxMessages.AnyAsync(x => x.Id == eventId));
    }

    [Fact]
    public async Task Dispatcher_failure_prevents_command_save_and_preserves_domain_event()
    {
        Guid orderId;

        using (var scope = fixture.Services.CreateScope())
        {
            var services = scope.ServiceProvider;
            var dispatcher = new RecordingDispatcher(services.GetRequiredService<IDomainEventDispatcher>())
            {
                Fail = true
            };

            await Assert.ThrowsAsync<IOException>(() => Handler(services, dispatcher).Handle(Command(), default));
            var order = Assert.Single(services.GetRequiredService<OrdersDbContext>().Orders.Local);

            orderId = order.Id;
            Assert.Single(order.DomainEvents);
        }

        using var read = fixture.Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<OrdersDbContext>();

        Assert.False(await db.Orders.AnyAsync(x => x.Id == orderId));
        Assert.False(await db.OutboxMessages.AnyAsync(x => x.OrderingKey == orderId.ToString("N")));
    }

    private static CreateOrderCommandHandler Handler(
        IServiceProvider services,
        IDomainEventDispatcher dispatcher) => new(
            services.GetRequiredService<IRepository<Order, Guid>>(),
            dispatcher,
            services.GetRequiredService<IUnitOfWork>(),
            services.GetRequiredService<IDateTimeOffsetProvider>());

    private static CreateOrderCommand Command(string? externalId = null) => new(
        "domain-event-test",
        externalId ?? Guid.NewGuid().ToString("N"),
        new(
            "Company",
            "123",
            "a@b.com"),
        "RUB",
        [new(
            "SKU",
            "Item",
            2,
            10)]);

    private static Order Create(string? externalId = null) => Order.CreateOrder(
        Guid.NewGuid(),
        "domain-event-test",
        externalId ?? Guid.NewGuid().ToString("N"),
        new(
            "Company",
            "123",
            "a@b.com"),
        "RUB",
        DateTimeOffset.UtcNow,
        [new(
            Guid.NewGuid(),
            "SKU",
            "Item",
            2,
            10)]);

    private sealed class RecordingDispatcher(IDomainEventDispatcher inner) : IDomainEventDispatcher
    {
        public bool Fail { get; init; }

        public List<IDomainEvent> Handled { get; } = [];

        public async Task DispatchAsync(
            IDomainEvent domainEvent,
            CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new IOException("Simulated handler failure.");
            }

            await inner.DispatchAsync(domainEvent, cancellationToken);
            Handled.Add(domainEvent);
        }
    }
}
