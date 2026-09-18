using OrderBridge.Application.Features.OrderIntegrations.Services.Interfaces;
using OrderBridge.Domain.ValueObjects;

namespace OrderBridge.Application.Features.OrderIntegrations.Services.Implementations;

public sealed class OrderIntegrationResolver : IOrderIntegrationResolver
{
    private readonly IReadOnlyDictionary<
        (IntegrationTarget Target, IntegrationOperation Operation),
        IOrderIntegrationHandler> _handlers;

    public OrderIntegrationResolver(IEnumerable<IOrderIntegrationHandler> handlers)
    {
        _handlers = handlers.ToDictionary(handler =>
            (handler.IntegrationTarget, handler.IntegrationOperation));
    }

    public IOrderIntegrationHandler Resolve(
        IntegrationTarget target,
        IntegrationOperation operation)
    {
        if (_handlers.TryGetValue((target, operation), out var handler))
            return handler;

        throw new InvalidOperationException(
            $"No integration handler is registered for target '{target}' and operation '{operation}'.");
    }
}
