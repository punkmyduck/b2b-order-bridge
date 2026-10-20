using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrderBridge.Contracts.Events;
using OrderBridge.Orders.Domain.Models;
using OrderBridge.Orders.Domain.ValueObjects;
using Shared.Messaging;
using Shared.Messaging.Kafka;
using Shared.Messaging.Persistence;
using Microsoft.Extensions.Options;
using OrderBridge.Orders.Infrastructure.Persistence;
using OrderBridge.Orders.Infrastructure.Services;
using Xunit;
using Shared.Messaging.Abstractions.Publishing;
using Shared.Messaging.Abstractions.Events;

namespace OrderBridge.Orders.Infrastructure.Tests;

public sealed class OutboxTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    [Fact]
    public async Task Order_and_outbox_roll_back_together_on_constraint_failure()
    {
        var message = await SeedAsync();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var duplicate = CreateOrder(message.Payload.Order.ExternalOrderId);

            db.Orders.Add(duplicate);
            new EfEventOutbox<OrdersDbContext>(db, Options.Create(new EventRouteOptions())).Add(OrderCreatedEvent.Definition, ToEvent(duplicate));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        await using var read = fixture.Services.CreateAsyncScope();
        var saved = read.ServiceProvider.GetRequiredService<OrdersDbContext>();

        Assert.Equal(
            1,
            await saved.Orders.CountAsync(x => x.ExternalOrderId == message.Payload.Order.ExternalOrderId));
        Assert.Equal(
            1,
            await saved.OutboxMessages.CountAsync(x => x.OrderingKey == message.Payload.Order.Id.ToString("N")));
    }

    [Fact]
    public async Task Publish_failure_leaves_event_pending_and_order_committed()
    {
        var message = await SeedAsync();
        var publisher = new RecordingPublisher
        {
            Fail = true
        };

        await DispatchDueAsync(publisher);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var saved = await db.OutboxMessages.SingleAsync(x => x.Id == message.Metadata.EventId);

        Assert.Null(saved.PublishedAt);
        Assert.True(saved.PublishAttempts > 0);
        Assert.NotNull(saved.LastError);
        Assert.True(saved.NextAttemptAt > saved.OccurredAt);
        Assert.True(await db.Orders.AnyAsync(x => x.Id == message.Payload.Order.Id));
    }

    [Fact]
    public async Task Acknowledged_event_is_marked_published_and_contains_original_identity()
    {
        var message = await SeedAsync();
        var publisher = new RecordingPublisher();

        await DispatchDueAsync(publisher);
        var sent = Assert.Single(publisher.Messages, x => x.Key == message.Payload.Order.Id.ToString("N"));
        var payload = EventJson.Deserialize(sent.Payload, OrderCreatedEvent.Definition);

        Assert.Equal(message.Metadata.EventId, payload.Metadata.EventId);
        Assert.Equal(message.Payload.Order.Id, payload.Payload.Order.Id);
        Assert.Single(payload.Payload.Order.Lines);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        Assert.NotNull((await db.OutboxMessages.SingleAsync(x => x.Id == message.Metadata.EventId)).PublishedAt);
    }

    [Fact]
    public async Task Migration_preserves_pending_legacy_event_and_supports_downgrade()
    {
        await using var postgres = new Testcontainers.PostgreSql.PostgreSqlBuilder("postgres:17-alpine").Build();

        await postgres.StartAsync();
        await using var db = new OrdersDbContext(new DbContextOptionsBuilder<OrdersDbContext>().UseNpgsql(postgres.GetConnectionString()).Options);
        var migrator = db.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>();

        await migrator.MigrateAsync("20261019143357_InitialOrders");
        var order = CreateOrder();
        var id = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var old = JsonSerializer.Serialize(
            new { eventId = id, occurredAt = now, processingScenario = "FullCycle", order = ToEvent(order).Payload.Order },
            EventJson.Options);

        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "OutboxMessages" ("Id", "OrderId", "EventType", "Payload", "OccurredAt", "NextAttemptAt", "PublishAttempts")
            VALUES ({id}, {order.Id}, 'order.created.v1', CAST({old} AS jsonb), {now}, {now}, 2)
            """);
        await migrator.MigrateAsync();
        var saved = await db.OutboxMessages.SingleAsync();

        Assert.Equal(id, saved.Id);
        Assert.Equal(2, saved.PublishAttempts);
        Assert.Null(saved.PublishedAt);
        Assert.Equal(order.Id.ToString("N"), saved.OrderingKey);
        var normalized = EventJson.Deserialize(saved.Payload, OrderCreatedEvent.Definition);

        Assert.Equal(id, normalized.Metadata.EventId);
        Assert.Equal(order.Id, normalized.Payload.Order.Id);
        db.ChangeTracker.Clear();
        await migrator.MigrateAsync("20261019143357_InitialOrders");
        var restored = await db.Database.SqlQueryRaw<Guid>("""SELECT "OrderId" AS "Value" FROM "OutboxMessages" AS o""").SingleAsync();

        Assert.Equal(order.Id, restored);
        await migrator.MigrateAsync();
    }

    [Fact]
    public async Task Not_before_schedule_and_custom_route_are_persisted_without_publishing_early()
    {
        var order = CreateOrder();
        var message = ToEvent(order);
        var due = DateTimeOffset.UtcNow.AddHours(1);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var routes = new EventRouteOptions();

        routes.Topics[OrderCreatedEvent.Definition.EventType] = "custom-orders";
        new EfEventOutbox<OrdersDbContext>(db, Options.Create(routes)).Add(
            OrderCreatedEvent.Definition,
            message,
            new(due));
        await db.SaveChangesAsync();
        var saved = await db.OutboxMessages.SingleAsync(x => x.Id == message.Metadata.EventId);

        Assert.Equal("custom-orders", saved.Topic);
        Assert.Equal(due, saved.NextAttemptAt);
        var transport = new RecordingPublisher();

        await DispatchDueAsync(transport);
        Assert.DoesNotContain(transport.Messages, x => x.Key == message.Metadata.OrderingKey);
    }

    private async Task DispatchDueAsync(RecordingPublisher publisher)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var dispatcher = new OutboxBatchPublisher<OrdersDbContext>(
            db,
            publisher,
            TimeProvider.System,
            NullLogger<OutboxBatchPublisher<OrdersDbContext>>.Instance);

        for (var i = 0; i < 50 && await dispatcher.PublishNextAsync(default); i++)
        {
        }
    }

    private async Task<IntegrationEvent<OrderCreatedV1>> SeedAsync()
    {
        var order = CreateOrder();
        var message = ToEvent(order);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();

        db.Orders.Add(order);
        new EfEventOutbox<OrdersDbContext>(db, Options.Create(new EventRouteOptions())).Add(OrderCreatedEvent.Definition, message);
        await db.SaveChangesAsync();

        return message;
    }

    private static Order CreateOrder(string? externalId = null) => Order.CreateOrder(
        Guid.NewGuid(),
        "messaging-tests",
        externalId ?? Guid.NewGuid().ToString("N"),
        new(
            "Test",
            "123",
            "test@example.com"),
        "RUB",
        DateTimeOffset.UtcNow,
        [new(
            Guid.NewGuid(),
            "SKU",
            "Item",
            1,
            10)]);

    private static IntegrationEvent<OrderCreatedV1> ToEvent(Order order) => IntegrationEvent<OrderCreatedV1>.Create(
        OrderCreatedEvent.Definition,
        new(
            "FullCycle",
            new(
                order.Id,
                order.SourceSystem,
                order.ExternalOrderId,
                new(
                    order.Customer.CompanyName,
                    order.Customer.TaxId,
                    order.Customer.ContactEmail),
                order.Currency,
                order.TotalAmount,
                order.Lines.Select(x => new OrderLineV1(
                    x.Id,
                    x.Sku,
                    x.Name,
                    x.Quantity,
                    x.UnitPrice,
                    x.LineTotal))
                    .ToArray())),
        "orders",
        order.Id.ToString("N"),
        order.CreatedAt);

    private sealed class RecordingPublisher : IEventTransport
    {
        public bool Fail { get; init; }

        public List<(string Key, string Payload)> Messages { get; } = [];

        public Task PublishAsync(
            string topic,
            string key,
            string payload,
            CancellationToken cancellationToken)
        {
            if (Fail)
            {
                throw new IOException("Broker is unavailable.");
            }

            Messages.Add((key, payload));

            return Task.CompletedTask;
        }
    }
}
