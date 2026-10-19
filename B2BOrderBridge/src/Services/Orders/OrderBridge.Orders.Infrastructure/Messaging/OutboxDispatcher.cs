using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrderBridge.Orders.Infrastructure.Persistence;
using Shared.Application.Services;

namespace OrderBridge.Orders.Infrastructure.Messaging;

public sealed class OutboxDispatcher(IServiceScopeFactory scopes, ILogger<OutboxDispatcher> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                // Bound each pass so a busy outbox still observes shutdown promptly.
                for (var i = 0; i < 20; i++)
                    if (!await scope.ServiceProvider.GetRequiredService<OutboxBatchPublisher>().PublishNextAsync(stoppingToken))
                        break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
            catch (Exception ex) { logger.LogError(ex, "Outbox dispatch pass failed; events remain in PostgreSQL"); }

            try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { return; }
        }
    }
}

public sealed class OutboxBatchPublisher(
    OrdersDbContext db, IKafkaEventPublisher publisher, KafkaOptions options,
    IDateTimeOffsetProvider clock, ILogger<OutboxBatchPublisher> logger)
{
    public async Task<bool> PublishNextAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.UtcNow;
        // Locks are held until the broker acknowledgement and DB commit. SKIP LOCKED permits other publishers.
        var messages = await db.OutboxMessages.FromSqlInterpolated($"""
            SELECT * FROM "OutboxMessages"
            WHERE "PublishedAt" IS NULL AND "NextAttemptAt" <= {now}
            ORDER BY "OccurredAt", "Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(cancellationToken);
        if (messages.Count == 0) return false;

        var message = messages[0];
        message.PublishAttempts++;
        try
        {
            await publisher.PublishAsync(options.OrderCreatedTopic, message.OrderId.ToString("N"), message.Payload, cancellationToken);
            message.PublishedAt = clock.UtcNow;
            message.LastError = null;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            message.NextAttemptAt = clock.UtcNow.AddSeconds(10);
            message.LastError = ex.Message.Length <= 2048 ? ex.Message : ex.Message[..2048];
            logger.LogWarning(ex, "Event {EventId} is still pending in outbox", message.Id);
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        db.Entry(message).State = EntityState.Detached;
        return true;
    }
}
