using OrderBridge.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.ValueObjects;

namespace OrderBridge.Application.Features.OrderIntegrations.Services.Interfaces;

public interface IOrderIntegrationHandler
{
    IntegrationTarget IntegrationTarget { get; }
    IntegrationOperation IntegrationOperation { get; }

    Task<IntegrationExecutionResult> ExecuteAsync(
        IntegrationExecutionContext executionContext,
        CancellationToken cancellationToken);
}
