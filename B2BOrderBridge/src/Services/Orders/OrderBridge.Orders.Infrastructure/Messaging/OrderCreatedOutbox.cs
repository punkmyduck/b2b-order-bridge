using System.Text.Json;
using OrderBridge.Orders.Application.Messaging;
using OrderBridge.Contracts.Events;
using OrderBridge.Orders.Infrastructure.Persistence;

namespace OrderBridge.Orders.Infrastructure.Messaging;

public sealed class OrderCreatedOutbox(OrdersDbContext db) : IOrderCreatedOutbox
{
    public void Add(OrderCreatedV1 message)
    {
        db.OutboxMessages.Add(new OutboxMessage
        {
            Id = message.EventId,
            OrderId = message.Order.Id,
            EventType = OrderCreatedV1.EventType,
            OccurredAt = message.OccurredAt,
            NextAttemptAt = message.OccurredAt,
            Payload = JsonSerializer.Serialize(message, EventJson.Options)
        });
    }
}

public static class EventJson
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.Web);
}
