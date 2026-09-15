using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.Models;

namespace OrderBridge.Application.Features.OrderIntegrations.Services;

public interface IOrderIntegrationPlanner
{
    IReadOnlyList<OrderIntegration> Create(Order order, OrderProcessingScenario scenario, DateTimeOffset now);
}
