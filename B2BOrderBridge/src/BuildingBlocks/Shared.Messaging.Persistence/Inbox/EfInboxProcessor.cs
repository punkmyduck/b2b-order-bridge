using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Shared.Messaging.Abstractions.Events;
using Shared.Messaging.Abstractions.Processing;

namespace Shared.Messaging.Persistence;

public sealed class EfInboxProcessor<TDbContext>(
    TDbContext db,
    TimeProvider clock) : IInboxProcessor
    where TDbContext : DbContext
{
    public async Task ProcessAsync<T>(
        string handlerId,
        IntegrationEvent<T> message,
        Func<CancellationToken, Task> handle,
        CancellationToken cancellationToken)
        where T : class
    {
        // The handler and inbox share this scoped DbContext and transaction.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var metadata = message.Metadata;
        var json = JsonSerializer.Serialize(message, MessagingJson.Options);
        var now = clock.GetUtcNow();

        var inserted = await db.Database.ExecuteSqlInterpolatedAsync(
            $"""
            INSERT INTO "InboxMessages"
                ("Consumer", "EventId", "Source", "EventType", "IdempotencyKey", "OrderingKey", "Payload", "ProcessedAt")
            VALUES ({handlerId}, {metadata.EventId}, {metadata.Source}, {metadata.EventType}, {metadata.IdempotencyKey}, {metadata.OrderingKey}, CAST({json} AS jsonb), {now})
            ON CONFLICT DO NOTHING
            """,
            cancellationToken);

        if (inserted == 0)
        {
            return;
        }

        await handle(cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
