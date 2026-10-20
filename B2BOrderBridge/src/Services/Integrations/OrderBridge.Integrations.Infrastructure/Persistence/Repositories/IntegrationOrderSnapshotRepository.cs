using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Repositories;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;
using Shared.Messaging.Persistence;

namespace OrderBridge.Integrations.Infrastructure.Persistence.Repositories;

public sealed class IntegrationOrderSnapshotRepository(IntegrationsDbContext db) : IIntegrationOrderSnapshotRepository
{
    public async Task<OrderExecutionSnapshot?> GetByIdAsync(
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        var payload = await db.IntegrationOrders
            .AsNoTracking()
            .Where(x => x.Id == orderId)
            .Select(x => x.Payload)
            .SingleOrDefaultAsync(cancellationToken);

        return payload is null ? null : JsonSerializer.Deserialize<OrderExecutionSnapshot>(payload, MessagingJson.Options);
    }
}
