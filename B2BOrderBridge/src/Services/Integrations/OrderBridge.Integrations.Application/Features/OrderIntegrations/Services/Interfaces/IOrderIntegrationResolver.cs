using OrderBridge.Integrations.Domain.ValueObjects;

namespace OrderBridge.Integrations.Application.Features.OrderIntegrations.Services.Interfaces
{
    public interface IOrderIntegrationResolver
    {
        IOrderIntegrationHandler Resolve(IntegrationTarget target, IntegrationOperation operation);
    }
}