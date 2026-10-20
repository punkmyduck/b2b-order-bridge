using OrderBridge.Orders.Application.Features.Orders.ValueObjects;
using OrderBridge.Orders.Application.ValueObjects;
using OrderBridge.Orders.Domain.Models;
using Shared.Application.Messaging;
using Shared.Application.Events;
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

    private readonly IDomainEventDispatcher _domainEventDispatcher;

    private readonly IUnitOfWork _unitOfWork;

    private readonly IDateTimeOffsetProvider _dateTimeOffsetProvider;

    public CreateOrderCommandHandler(
        IRepository<Order, Guid> orderRepository,
        IDomainEventDispatcher domainEventDispatcher,
        IUnitOfWork unitOfWork,
        IDateTimeOffsetProvider dateTimeOffsetProvider)
    {
        _orderRepository = orderRepository;
        _domainEventDispatcher = domainEventDispatcher;
        _unitOfWork = unitOfWork;
        _dateTimeOffsetProvider = dateTimeOffsetProvider;
    }

    public async Task<Result<CreateOrderResponse>> Handle(
        CreateOrderCommand command,
        CancellationToken cancellationToken)
    {
        var now = _dateTimeOffsetProvider.UtcNow;
        var lines = command.Lines.Select(line => new OrderLine(
            Guid.NewGuid(),
            line.Sku,
            line.Name,
            line.Quantity,
            line.UnitPrice)).ToList();
        var order = Order.CreateOrder(
            Guid.NewGuid(),
            command.SourceSystem,
            command.ExternalOrderId,
            CustomerSnapshotDto.ToDomain(command.Customer),
            command.Currency,
            now,
            lines);

        _orderRepository.Add(order);

        foreach (var domainEvent in order.DomainEvents)
        {
            await _domainEventDispatcher.DispatchAsync(domainEvent, cancellationToken);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        order.ClearDomainEvents();

        return CreateOrderResponse.FromDomain(order);
    }
}
