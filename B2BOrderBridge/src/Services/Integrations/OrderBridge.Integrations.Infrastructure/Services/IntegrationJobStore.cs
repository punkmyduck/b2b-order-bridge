using Microsoft.EntityFrameworkCore;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Services.Interfaces;
using OrderBridge.Integrations.Domain.ValueObjects;
using OrderBridge.Integrations.Infrastructure.Persistence;

namespace OrderBridge.Integrations.Infrastructure.Services;

public sealed class IntegrationJobStore(IntegrationsDbContext context) : IIntegrationJobStore
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
