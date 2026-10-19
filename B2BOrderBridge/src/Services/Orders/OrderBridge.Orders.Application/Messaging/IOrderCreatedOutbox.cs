using OrderBridge.Contracts.Events;

namespace OrderBridge.Orders.Application.Messaging;

public interface IOrderCreatedOutbox
{
    /// <summary>Stages the event in the same unit of work as the new order. Does not call Kafka.</summary>
    void Add(OrderCreatedV1 message);
}
