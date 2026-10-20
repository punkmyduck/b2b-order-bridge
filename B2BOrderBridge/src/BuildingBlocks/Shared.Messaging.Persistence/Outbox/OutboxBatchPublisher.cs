using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Shared.Messaging.Abstractions.Publishing;

namespace Shared.Messaging.Persistence;

public sealed class OutboxBatchPublisher<TDbContext>(
    TDbContext db,
    IEventTransport publisher,
    TimeProvider clock,
    ILogger<OutboxBatchPublisher<TDbContext>> logger)
    where TDbContext : DbContext
{
    public async Task<bool> PublishNextAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.GetUtcNow();
        var message = await db.Set<OutboxMessage>().FromSqlInterpolated($"""
            SELECT * FROM "OutboxMessages"
            WHERE "PublishedAt" IS NULL AND "NextAttemptAt" <= {now}
            ORDER BY "OccurredAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """)
            .SingleOrDefaultAsync(cancellationToken);

        if (message is null)
        {
            return false;
        }

        message.PublishAttempts++;

        try
        {
            await publisher.PublishAsync(
                message.Topic,
                message.OrderingKey,
                message.Payload,
                cancellationToken);
            message.PublishedAt = clock.GetUtcNow();
            message.LastError = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            message.NextAttemptAt = clock.GetUtcNow().AddSeconds(10);
            message.LastError = ex.Message.Length <= 2048 ? ex.Message : ex.Message[..2048];
            logger.LogWarning(
                ex,
                "Event {EventId} remains pending in outbox",
                message.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.Entry(message).State = EntityState.Detached;

        return true;
    }
}
