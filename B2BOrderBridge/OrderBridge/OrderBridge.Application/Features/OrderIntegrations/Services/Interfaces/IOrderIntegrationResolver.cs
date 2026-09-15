using OrderBridge.Domain.ValueObjects;

namespace OrderBridge.Application.Features.OrderIntegrations.Services.Interfaces
{
    public interface IOrderIntegrationResolver
    {
        IOrderIntegrationHandler Resolve(IntegrationTarget target, IntegrationOperation operation);
    }
}