using OrderBridge.Application.Services.Interfaces;
using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.Models;
using OrderBridge.Domain.ValueObjects;

namespace OrderBridge.Application.Services.Implementations;

public sealed class OrderIntegrationPlanner : IOrderIntegrationPlanner
{
    public IReadOnlyList<OrderIntegration> Create(Order order, OrderProcessingScenario scenario, DateTimeOffset now)
    {
        return scenario switch
        {
            OrderProcessingScenario.Internal => [],

            OrderProcessingScenario.Accounting => [
                Create(IntegrationTarget.OneC, IntegrationOperation.CreateSalesOrder),
                ],

            OrderProcessingScenario.FullCycle => [
                Create(IntegrationTarget.Bitrix24, IntegrationOperation.CreateDeal),
                Create(IntegrationTarget.OneC, IntegrationOperation.CreateSalesOrder),
                Create(IntegrationTarget.Telegram, IntegrationOperation.SendOrderReceivedNotification),
                Create(IntegrationTarget.Diadoc, IntegrationOperation.CreateDocument, true),
                ],

            _ => throw new ArgumentOutOfRangeException(nameof(scenario))
        };


        OrderIntegration Create(
            IntegrationTarget target,
            IntegrationOperation operation,
            bool waiting = false)
        {
            var id = Guid.NewGuid();

            return OrderIntegration.Create(
                id,
                order,
                target,
                operation,
                idempotencyKey: id.ToString("N"),
                createdAt: now,
                isWaitingForPrerequisite: waiting);
        }
    }
}
