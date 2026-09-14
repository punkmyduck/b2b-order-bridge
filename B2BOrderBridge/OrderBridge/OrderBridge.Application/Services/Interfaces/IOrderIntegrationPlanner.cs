using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.Models;

namespace OrderBridge.Application.Services.Interfaces;

public interface IOrderIntegrationPlanner
{
    IReadOnlyList<OrderIntegration> Create(Order order, OrderProcessingScenario scenario, DateTimeOffset now);
}
