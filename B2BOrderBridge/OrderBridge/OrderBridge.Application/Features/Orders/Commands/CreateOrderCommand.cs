using OrderBridge.Application.Features.Orders.ValueObjects;
using OrderBridge.Application.Services.Interfaces;
using OrderBridge.Application.ValueObjects;
using OrderBridge.Domain.Models;
using Shared.Application.Messaging;
using Shared.Application.Persistence;
using Shared.Application.Results;
using Shared.Application.Services;

namespace OrderBridge.Application.Features.Orders.Commands;

public sealed record CreateOrderCommand(
    string SourceSystem,
    string ExternalOrderId,
    CustomerSnapshotDto Customer,
    string Currency,
    IReadOnlyCollection<OrderLineDto> Lines) : ICommand<CreateOrderResponse>;

public sealed class CreateOrderCommandHandler : ICommandHandler<CreateOrderCommand, CreateOrderResponse>
{
    private readonly IRepository<Order, Guid> _orderRepository;
    private readonly IRepository<OrderIntegration, Guid> _integrationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDateTimeOffsetProvider _dateTimeOffsetProvider;
    private readonly IOrderIntegrationPlanner _orderIntegrationPlanner;

    public CreateOrderCommandHandler(
        IRepository<Order, Guid> orderRepository,
        IRepository<OrderIntegration, Guid> integrationRepository,
        IUnitOfWork unitOfWork,
        IDateTimeOffsetProvider dateTimeOffsetProvider,
        IOrderIntegrationPlanner orderIntegrationPlanner)
    {
        _orderRepository = orderRepository;
        _integrationRepository = integrationRepository;
        _unitOfWork = unitOfWork;
        _dateTimeOffsetProvider = dateTimeOffsetProvider;
        _orderIntegrationPlanner = orderIntegrationPlanner;
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

        var integrations = _orderIntegrationPlanner.Create(
            order,
            OrderProcessingScenario.FullCycle,
            now);

        _orderRepository.Add(order);
        foreach (var integration in integrations)
            _integrationRepository.Add(integration);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return CreateOrderResponse.FromDomain(order);
    }
}
