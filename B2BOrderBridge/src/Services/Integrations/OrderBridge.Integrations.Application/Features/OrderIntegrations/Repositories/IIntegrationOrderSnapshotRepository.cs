using OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;

namespace OrderBridge.Integrations.Application.Features.OrderIntegrations.Repositories;

public interface IIntegrationOrderSnapshotRepository
{
    Task<OrderExecutionSnapshot?> GetByIdAsync(Guid orderId, CancellationToken cancellationToken = default);
}
