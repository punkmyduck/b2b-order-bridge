using OrderBridge.Orders.Application.Messaging;
using OrderBridge.Contracts.Events;
using OrderBridge.Orders.Application.Features.Orders.ValueObjects;
using OrderBridge.Orders.Application.ValueObjects;
using OrderBridge.Orders.Domain.Models;
using Shared.Application.Messaging;
using Shared.Application.Persistence;
using Shared.Application.Results;
using Shared.Application.Services;

namespace OrderBridge.Orders.Application.Features.Orders.Commands;

public sealed record CreateOrderCommand(
    string SourceSystem,
    string ExternalOrderId,
    CustomerSnapshotDto Customer,
    string Currency,
    IReadOnlyCollection<OrderLineDto> Lines) : ICommand<CreateOrderResponse>;

public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, CreateOrderResponse>
{
    private readonly IRepository<Order, Guid> _orderRepository;
    private readonly IOrderCreatedOutbox _outbox;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeOffsetProvider _dateTimeOffsetProvider;

    public CreateOrderCommandHandler(
        IRepository<Order, Guid> orderRepository,
        IOrderCreatedOutbox outbox,
        IUnitOfWork unitOfWork,
        IDateTimeOffsetProvider dateTimeOffsetProvider)
    {
        _orderRepository = orderRepository;
        _outbox = outbox;
        _unitOfWork = unitOfWork;
        _dateTimeOffsetProvider = dateTimeOffsetProvider;
    }

    public async Task<Result<CreateOrderResponse>> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var now = _dateTimeOffsetProvider.UtcNow;
        var lines = command.Lines
            .Select(line => new OrderLine(
                Guid.NewGuid(),
                line.Sku,
                line.Name,
                line.Quantity,
                line.UnitPrice))
            .ToList();

        var order = Order.CreateOrder(
            Guid.NewGuid(),
            command.SourceSystem,
            command.ExternalOrderId,
            CustomerSnapshotDto.ToDomain(command.Customer),
            command.Currency,
            now,
            lines);

        _orderRepository.Add(order);
        _outbox.Add(new OrderCreatedV1(
            Guid.NewGuid(), now, OrderProcessingScenario.FullCycle.ToString(),
            new OrderSnapshotV1(order.Id, order.SourceSystem, order.ExternalOrderId,
                new CustomerSnapshotV1(order.Customer.CompanyName, order.Customer.TaxId, order.Customer.ContactEmail),
                order.Currency, order.TotalAmount,
                order.Lines.Select(line => new OrderLineV1(line.Id, line.Sku, line.Name,
                    line.Quantity, line.UnitPrice, line.LineTotal)).ToArray())));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CreateOrderResponse.FromDomain(order);
    }
}
