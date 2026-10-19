using OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Integrations.Application.ValueObjects;
using OrderBridge.Integrations.Domain.ValueObjects;

namespace OrderBridge.Integrations.Application.Features.OrderIntegrations.Services.Interfaces;

public interface IOrderIntegrationHandler
{
    IntegrationTarget IntegrationTarget { get; }
    IntegrationOperation IntegrationOperation { get; }

    Task<IntegrationExecutionResult> ExecuteAsync(
        IntegrationExecutionContext executionContext,
        CancellationToken cancellationToken);
}
