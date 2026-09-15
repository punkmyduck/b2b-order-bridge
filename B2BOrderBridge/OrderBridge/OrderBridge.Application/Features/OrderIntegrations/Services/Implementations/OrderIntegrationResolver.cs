using OrderBridge.Domain.ValueObjects;

namespace OrderBridge.Application.Features.OrderIntegrations.Services;

public class OrderIntegrationResolver : IOrderIntegrationResolver
{
    private Dictionary<(IntegrationTarget Target, IntegrationOperation Operation), IOrderIntegrationHandler> _handlers = new();

    public OrderIntegrationResolver(IEnumerable<IOrderIntegrationHandler> handlers)
    {
        _handlers = handlers.ToDictionary(x => (x.IntegrationTarget, x.IntegrationOperation));
    }

    public IOrderIntegrationHandler Resolve(IntegrationTarget target, IntegrationOperation operation)
    {
        if (_handlers.TryGetValue((target, operation), out var handler))
        {
            return handler;
        }

        throw new InvalidOperationException($"Integration handler not found. Target: {target}, Operation: {operation}");
    }
}
