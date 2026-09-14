using OrderBridge.Domain.Models;
using OrderBridge.Domain.ValueObjects;
using Shared.Application.Messaging;
using Shared.Application.Persistence;
using Shared.Application.Results;
using Shared.Application.Services;

namespace OrderBridge.Application.Features.Orders.Commands;

public sealed record CreateOrderCommand(
    string SourceSystem,
    string ExternalOrderId,
    CustomerSnapshot CustomerSnapshot,
    string Currency,
    List<OrderLine> OrderLines) : ICommand<Guid>;

public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, Guid>
{
    private readonly IRepository<Order, Guid> _orderRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeOffsetProvider _dateTimeOffsetProvider;

    public CreateOrderCommandHandler(
        IRepository<Order, Guid> orderRepository,
        IUnitOfWork unitOfWork,
        IDateTimeOffsetProvider dateTimeOffsetProvider)
    {
        _orderRepository = orderRepository;
        _unitOfWork = unitOfWork;
        _dateTimeOffsetProvider = dateTimeOffsetProvider;
    }

    public async Task<Result<Guid>> Handle(CreateOrderCommand command, CancellationToken cancellationToken)
    {
        var now = _dateTimeOffsetProvider.UtcNow;

        var order = Order.CreateOrder(
            Guid.NewGuid(),
            command.SourceSystem,
            command.ExternalOrderId,
            command.CustomerSnapshot,
            command.Currency,
            now,
            command.OrderLines);

        _orderRepository.Add(order);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return order.Id;
    }
}

