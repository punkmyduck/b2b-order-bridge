using OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Integrations.Domain.Models;

namespace OrderBridge.Integrations.Application.Messaging;

public interface IIntegrationPlanningStore
{
    /// <summary>Atomically persists the inbox receipt, local order projection and planned integrations.</summary>
    Task SaveOnceAsync(Guid eventId, OrderExecutionSnapshot order, IReadOnlyList<OrderIntegration> integrations,
        CancellationToken cancellationToken);
}
