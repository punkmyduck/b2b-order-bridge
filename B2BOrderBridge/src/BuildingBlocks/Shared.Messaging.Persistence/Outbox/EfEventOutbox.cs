using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Shared.Messaging.Abstractions.Events;
using Shared.Messaging.Abstractions.Publishing;

namespace Shared.Messaging.Persistence;

public sealed class EfEventOutbox<TDbContext>(
    TDbContext db,
    IOptions<EventRouteOptions> routes) : IEventOutbox
    where TDbContext : DbContext
{
    public void Add<T>(
        EventDefinition<T> definition,
        IntegrationEvent<T> message,
        OutboxDeliveryOptions? delivery = null)
        where T : class
    {
        EventValidation.Validate(message, definition);
        var metadata = message.Metadata;

        db.Set<OutboxMessage>().Add(
            new OutboxMessage
            {
                Id = metadata.EventId,
                EventType = metadata.EventType,
                Topic = routes.Value.Resolve(definition),
                OrderingKey = metadata.OrderingKey,
                IdempotencyKey = metadata.IdempotencyKey,
                OccurredAt = metadata.OccurredAt.ToUniversalTime(),
                NextAttemptAt = (delivery?.NotBefore ?? metadata.OccurredAt).ToUniversalTime(),
                Payload = JsonSerializer.Serialize(message, MessagingJson.Options)
            });
    }
}
