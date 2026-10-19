using OrderBridge.Integrations.Application.ValueObjects;
using OrderBridge.Integrations.Domain.Models;

namespace OrderBridge.Integrations.Application.Features.OrderIntegrations.Services.Interfaces;

public interface IOrderIntegrationPlanner
{
    IReadOnlyList<OrderIntegration> Create(Guid orderId, OrderProcessingScenario scenario, DateTimeOffset now);
}
