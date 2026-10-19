using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Integrations.Application.Messaging;
using OrderBridge.Integrations.Domain.Models;
using OrderBridge.Integrations.Infrastructure.Messaging;
using Shared.Application.Services;

namespace OrderBridge.Integrations.Infrastructure.Persistence;

public sealed class IntegrationPlanningStore(IntegrationsDbContext db, IDateTimeOffsetProvider clock) : IIntegrationPlanningStore
{
    public const string ConsumerName = "order-created-integration-planner-v1";

    public async Task SaveOnceAsync(Guid eventId, OrderExecutionSnapshot order,
        IReadOnlyList<OrderIntegration> integrations, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var now = clock.UtcNow;
        var inserted = await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "InboxMessages" ("Consumer", "EventId", "ProcessedAt")
            VALUES ({ConsumerName}, {eventId}, {now}) ON CONFLICT DO NOTHING
            """, cancellationToken);
        if (inserted == 0) return;

        var payload = JsonSerializer.Serialize(order, EventJson.Options);
        // First accepted snapshot is immutable; a redelivery cannot overwrite execution inputs.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "IntegrationOrders" ("Id", "Payload", "ReceivedAt")
            VALUES ({order.Id}, CAST({payload} AS jsonb), {now}) ON CONFLICT ("Id") DO NOTHING
            """, cancellationToken);
        var existing = await db.OrderIntegrations.Where(x => x.OrderId == order.Id)
            .Select(x => new { x.IntegrationTarget, x.IntegrationOperation }).ToListAsync(cancellationToken);
        foreach (var integration in integrations)
            if (!existing.Any(x => x.IntegrationTarget == integration.IntegrationTarget && x.IntegrationOperation == integration.IntegrationOperation))
                db.OrderIntegrations.Add(integration);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }
}
