using OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Integrations.Domain.Models;

namespace OrderBridge.Integrations.Application.Messaging;

public interface IIntegrationPlanningStore
{
    /// <summary>Persists the local projection and jobs inside the inbox transaction.</summary>
    Task SaveAsync(OrderExecutionSnapshot order, IReadOnlyList<OrderIntegration> integrations,
        CancellationToken cancellationToken);
}
