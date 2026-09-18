using Microsoft.EntityFrameworkCore;
using OrderBridge.Application.Features.OrderIntegrations.Services.Interfaces;
using OrderBridge.Domain.ValueObjects;
using OrderBridge.Infrastructure.Persistence;

namespace OrderBridge.Infrastructure.Services;

public sealed class IntegrationJobStore(OrderBridgeDbContext context) : IIntegrationJobStore
{
    public async Task<IReadOnlyList<Guid>> GetReadyIntegrationIdsAsync(
        DateTimeOffset now,
        int batchSize,
        CancellationToken cancellationToken)
    {
        if (batchSize <= 0)
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be greater than zero.");

        return await context.OrderIntegrations
            .AsNoTracking()
            .Where(integration =>
                integration.IntegrationStatus == IntegrationStatus.Pending ||
                integration.IntegrationStatus == IntegrationStatus.RetryScheduled &&
                integration.NextAttemptAt != null &&
                integration.NextAttemptAt <= now)
            .OrderBy(integration => integration.NextAttemptAt ?? integration.CreatedAt)
            .ThenBy(integration => integration.CreatedAt)
            .Select(integration => integration.Id)
            .Take(batchSize)
            .ToListAsync(cancellationToken);
    }
}
