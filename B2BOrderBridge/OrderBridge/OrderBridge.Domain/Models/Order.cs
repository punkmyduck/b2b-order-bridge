using OrderBridge.Domain.Events;
using OrderBridge.Domain.Exceptions;
using OrderBridge.Domain.ValueObjects;
using Shared.Domain;

namespace OrderBridge.Domain.Models;

public class Order : AggregateRoot<Guid>
{
    public const int SourceSystemMaxLength = 64;
    public const int ExternalOrderIdMaxLength = 512;
    public const int CurrencyMaxLength = CurrencyCodes.CodeLength;


    public string SourceSystem { get; private set; } = null!;
    public string ExternalOrderId { get; private set; } = null!;

    public CustomerSnapshot Customer { get; private set; } = null!;

    public OrderStatus OrderStatus { get; private set; }
    public PaymentStatus PaymentStatus { get; private set; }

    public decimal TotalAmount { get; private set; }
    public string Currency { get; private set; } = null!;

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    public IReadOnlyCollection<OrderLine> Lines => _lines.AsReadOnly();
    private List<OrderLine> _lines = new();

    private Order() { }

    private Order(
        Guid id,
        string sourceSystem,
        string externalOrderId,
        CustomerSnapshot customer,
        OrderStatus orderStatus,
        PaymentStatus paymentStatus,
        decimal totalAmount,
        string currency,
        List<OrderLine> orderLines,
        DateTimeOffset createdAt,
        DateTimeOffset? updatedAt = null)
    {
        Id = id;
        SourceSystem = sourceSystem;
        ExternalOrderId = externalOrderId;
        Customer = customer;
        OrderStatus = orderStatus;
        PaymentStatus = paymentStatus;
        TotalAmount = totalAmount;
        Currency = currency;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        _lines = orderLines;
    }

    public static Order CreateOrder(
        Guid id,
        string sourceSystem,
        string externalOrderId,
        CustomerSnapshot customer,
        string currency,
        DateTimeOffset createdAt,
        List<OrderLine> orderLines)
    {
        if (orderLines is null) throw new ArgumentNullException(nameof(orderLines));
        if (customer is null) throw new ArgumentNullException(nameof(customer));

        var lines = orderLines.ToList();

        if (lines.Any(line => line is null))
            throw new DomainValidationException("Order.OrderLines.Null", "Order lines must not contain null.");

        if (lines.Select(line => line.Id).Distinct().Count() != lines.Count)
            throw new DomainValidationException("Order.OrderLines.DuplicateId", "Order line identifiers must be unique.");

        if (lines.Count == 0) 
            throw new DomainValidationException("Order.OrderLines.Empty", "Order must contain at least one line.");

        var order = new Order(
            Guard.NotEmpty(id, nameof(Id)),
            Guard.RequiredString(sourceSystem, maxLength: SourceSystemMaxLength, 1, nameof(SourceSystem)),
            Guard.RequiredString(externalOrderId, maxLength: ExternalOrderIdMaxLength),
            customer,
            OrderStatus.Received,
            PaymentStatus.Pending,
            Guard.Money(lines.Sum(s => s.LineTotal)),
            CurrencyCodes.Normalize(currency),
            lines,
            createdAt);

        return order;
    }

    public void Cancel(DateTimeOffset canceledAt)
    {
        if (OrderStatus == OrderStatus.Cancelled) return;

        UpdatedAt = canceledAt;
        OrderStatus = OrderStatus.Cancelled;

        RaiseDomainEvent(new OrderCancelledEvent(Guid.NewGuid(), canceledAt, this));
    }

    public void PayForOrder(DateTimeOffset paidAt)
    {
        if (PaymentStatus == PaymentStatus.Paid) return;

        UpdatedAt = paidAt;
        PaymentStatus = PaymentStatus.Paid;

        if (OrderStatus == OrderStatus.Cancelled)
        {
            RaiseDomainEvent(new PaidCancelledOrderEvent(Guid.NewGuid(), paidAt, this));
        }
    }
}


