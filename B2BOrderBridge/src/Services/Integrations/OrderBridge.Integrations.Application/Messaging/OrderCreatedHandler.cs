using OrderBridge.Contracts.Events;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Services.Interfaces;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Integrations.Application.ValueObjects;
using Shared.Messaging;
using Shared.Messaging.Abstractions.Events;
using Shared.Messaging.Abstractions.Processing;

namespace OrderBridge.Integrations.Application.Messaging;

public sealed class OrderCreatedHandler(
    IIntegrationPlanningStore store,
    IOrderIntegrationPlanner planner) : IEventHandler<OrderCreatedV1>
{
    public Task HandleAsync(
        IntegrationEvent<OrderCreatedV1> message,
        CancellationToken cancellationToken)
    {
        var payload = message.Payload;
        var scenario = ReadScenario(payload);

        if (message.Metadata.OrderingKey != payload.Order.Id.ToString("N"))
        {
            throw new InvalidEventException("The order ID must match the event ordering key.");
        }

        var order = OrderExecutionSnapshot.FromEvent(payload.Order);
        var integrations = planner.Create(
            order.Id,
            scenario,
            message.Metadata.OccurredAt);

        return store.SaveAsync(
            order,
            integrations,
            cancellationToken);
    }

    public static OrderProcessingScenario ReadScenario(OrderCreatedV1 payload)
    {
        if (payload.Order is not { } order ||
            order.Id == Guid.Empty ||
            order.Customer is null ||
            string.IsNullOrWhiteSpace(order.SourceSystem) ||
            string.IsNullOrWhiteSpace(order.ExternalOrderId) ||
            string.IsNullOrWhiteSpace(order.Currency) ||
            order.TotalAmount < 0 ||
            string.IsNullOrWhiteSpace(order.Customer.CompanyName) ||
            string.IsNullOrWhiteSpace(order.Customer.TaxId) ||
            string.IsNullOrWhiteSpace(order.Customer.ContactEmail) ||
            order.Lines is null ||
            order.Lines.Count == 0 ||
            order.Lines.Any(x => x is null ||
                x.Id == Guid.Empty ||
                string.IsNullOrWhiteSpace(x.Sku) ||
                string.IsNullOrWhiteSpace(x.Name) ||
                x.Quantity <= 0 ||
                x.UnitPrice < 0 ||
                x.LineTotal < 0) ||
            !Enum.TryParse<OrderProcessingScenario>(payload.ProcessingScenario, out var scenario) ||
            !Enum.IsDefined(scenario) ||
            Enum.GetName(scenario) != payload.ProcessingScenario)
        {
            throw new InvalidEventException("Invalid order payload or unsupported processing scenario.");
        }

        return scenario;
    }
}
