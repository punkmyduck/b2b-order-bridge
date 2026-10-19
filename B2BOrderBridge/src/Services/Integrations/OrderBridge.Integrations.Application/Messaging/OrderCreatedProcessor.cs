using System.Text.Json;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Services.Interfaces;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Integrations.Application.ValueObjects;
using OrderBridge.Contracts.Events;

namespace OrderBridge.Integrations.Application.Messaging;

public sealed class OrderCreatedProcessor(IIntegrationPlanningStore store, IOrderIntegrationPlanner planner)
{
    public Task ProcessAsync(OrderCreatedV1 message, CancellationToken cancellationToken)
    {
        var scenario = ReadScenario(message);
        var order = OrderExecutionSnapshot.FromEvent(message.Order);
        var integrations = planner.Create(order.Id, scenario, message.OccurredAt);
        return store.SaveOnceAsync(message.EventId, order, integrations, cancellationToken);
    }

    public static OrderProcessingScenario ReadScenario(OrderCreatedV1 message)
    {
        if (message.EventId == Guid.Empty || message.Order is null || message.Order.Id == Guid.Empty ||
            message.OccurredAt == default || message.Order.Customer is null || message.Order.Lines is null ||
            !Enum.TryParse<OrderProcessingScenario>(message.ProcessingScenario, out var scenario) ||
            !Enum.IsDefined(scenario) || Enum.GetName(scenario) != message.ProcessingScenario)
            throw new JsonException("Invalid order.created.v1 event or unsupported processing scenario.");
        return scenario;
    }
}
