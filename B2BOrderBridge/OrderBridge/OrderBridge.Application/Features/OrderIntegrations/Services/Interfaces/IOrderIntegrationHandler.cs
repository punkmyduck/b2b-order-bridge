using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.Models;
using OrderBridge.Domain.ValueObjects;

namespace OrderBridge.Application.Features.OrderIntegrations.Services;

public interface IOrderIntegrationHandler
{
    IntegrationTarget IntegrationTarget { get; }
    IntegrationOperation IntegrationOperation { get; }

    Task<IntegrationExecutionResult> ExecuteAsync(
        Order order,
        string idempotencyKey,
        CancellationToken cancellationToken);
}