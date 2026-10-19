using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using OrderBridge.Contracts.Events;
using OrderBridge.Orders.Domain.Models;
using OrderBridge.Orders.Domain.ValueObjects;
using OrderBridge.Orders.Infrastructure.Messaging;
using OrderBridge.Orders.Infrastructure.Persistence;
using OrderBridge.Orders.Infrastructure.Services;
using Xunit;
namespace OrderBridge.Orders.Infrastructure.Tests;
public sealed class OutboxTests(DatabaseFixture fixture) : IClassFixture<DatabaseFixture>
{
    private static readonly KafkaOptions Options = new("localhost:9092", "orders", "orders.dlq", "planner");
    [Fact]
    public async Task Order_and_outbox_roll_back_together_on_constraint_failure()
    {
        var message = await SeedAsync();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
            var duplicate = CreateOrder(message.Order.ExternalOrderId);
            db.Orders.Add(duplicate);
            new OrderCreatedOutbox(db).Add(ToEvent(duplicate));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        await using var read = fixture.Services.CreateAsyncScope();
        var saved = read.ServiceProvider.GetRequiredService<OrdersDbContext>();
        Assert.Equal(1, await saved.Orders.CountAsync(x => x.ExternalOrderId == message.Order.ExternalOrderId));
        Assert.Equal(1, await saved.OutboxMessages.CountAsync(x => x.OrderId == message.Order.Id));
    }

    [Fact]
    public async Task Publish_failure_leaves_event_pending_and_order_committed()
    {
        var message = await SeedAsync();
        var publisher = new RecordingPublisher { Fail = true };
        await DispatchDueAsync(publisher);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var saved = await db.OutboxMessages.SingleAsync(x => x.Id == message.EventId);
        Assert.Null(saved.PublishedAt);
        Assert.True(saved.PublishAttempts > 0);
        Assert.NotNull(saved.LastError);
        Assert.True(saved.NextAttemptAt > saved.OccurredAt);
        Assert.True(await db.Orders.AnyAsync(x => x.Id == message.Order.Id));
    }

    [Fact]
    public async Task Acknowledged_event_is_marked_published_and_contains_original_identity()
    {
        var message = await SeedAsync();
        var publisher = new RecordingPublisher();
        await DispatchDueAsync(publisher);
        var sent = Assert.Single(publisher.Messages, x => x.Key == message.Order.Id.ToString("N"));
        var payload = JsonSerializer.Deserialize<OrderCreatedV1>(sent.Payload, EventJson.Options)!;
        Assert.Equal(message.EventId, payload.EventId);
        Assert.Equal(message.Order.Id, payload.Order.Id);
        Assert.Single(payload.Order.Lines);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        Assert.NotNull((await db.OutboxMessages.SingleAsync(x => x.Id == message.EventId)).PublishedAt);
    }

    private async Task DispatchDueAsync(RecordingPublisher publisher)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        var dispatcher = new OutboxBatchPublisher(db, publisher, Options, new DateTimeOffsetProvider(),
            NullLogger<OutboxBatchPublisher>.Instance);
        for (var i = 0; i < 50 && await dispatcher.PublishNextAsync(default); i++) { }
    }

    private async Task<OrderCreatedV1> SeedAsync()
    {
        var order = CreateOrder();
        var message = ToEvent(order);
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<OrdersDbContext>();
        db.Orders.Add(order);
        new OrderCreatedOutbox(db).Add(message);
        await db.SaveChangesAsync();
        return message;
    }

    private static Order CreateOrder(string? externalId = null) => Order.CreateOrder(Guid.NewGuid(),
        "messaging-tests", externalId ?? Guid.NewGuid().ToString("N"), new("Test", "123", "test@example.com"),
        "RUB", DateTimeOffset.UtcNow, [new(Guid.NewGuid(), "SKU", "Item", 1, 10)]);

    private static OrderCreatedV1 ToEvent(Order order) => new(Guid.NewGuid(), order.CreatedAt, "FullCycle",
        new(order.Id, order.SourceSystem, order.ExternalOrderId,
            new(order.Customer.CompanyName, order.Customer.TaxId, order.Customer.ContactEmail), order.Currency, order.TotalAmount,
            order.Lines.Select(x => new OrderLineV1(x.Id, x.Sku, x.Name, x.Quantity, x.UnitPrice, x.LineTotal)).ToArray()));

    private sealed class RecordingPublisher : IKafkaEventPublisher
    {
        public bool Fail { get; init; }
        public List<(string Key, string Payload)> Messages { get; } = [];
        public Task PublishAsync(string topic, string key, string payload, CancellationToken cancellationToken)
        {
            if (Fail) throw new IOException("Broker is unavailable.");
            Messages.Add((key, payload));
            return Task.CompletedTask;
        }
    }
}
