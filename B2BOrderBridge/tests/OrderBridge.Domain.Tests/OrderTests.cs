using OrderBridge.Domain.Events;
using OrderBridge.Domain.Exceptions;
using OrderBridge.Domain.Models;
using OrderBridge.Domain.ValueObjects;
using Xunit;

namespace OrderBridge.Domain.Tests;

public sealed class OrderTests
{
    [Fact]
    public void Duplicate_line_ids_are_rejected_instead_of_discarded()
    {
        var id = Guid.NewGuid();
        var error = Assert.Throws<DomainValidationException>(() =>
            Create([Line(id, 1m, 10m), Line(id, 2m, 20m)]));

        Assert.Equal("Order.OrderLines.DuplicateId", error.Code);
    }

    [Fact]
    public void Null_and_empty_lines_are_rejected()
    {
        Assert.Throws<ArgumentNullException>(() => Create(null!));
        Assert.Equal("Order.OrderLines.Null",
            Assert.Throws<DomainValidationException>(() => Create([null!])).Code);
        Assert.Equal("Order.OrderLines.Empty",
            Assert.Throws<DomainValidationException>(() => Create([])).Code);
    }

    [Fact]
    public void Order_owns_its_list_and_sums_rounded_lines()
    {
        var lines = new List<OrderLine>
        {
            Line(Guid.NewGuid(), 0.25m, 0.10m),
            Line(Guid.NewGuid(), 0.25m, 0.10m)
        };
        var order = Create(lines);
        lines.Clear();

        Assert.Equal(2, order.Lines.Count);
        Assert.All(order.Lines, line => Assert.Equal(0.03m, line.LineTotal));
        Assert.Equal(0.06m, order.TotalAmount);
    }

    [Fact]
    public void Fractional_quantity_is_rounded_to_two_decimal_places()
    {
        Assert.Equal(0.03m, Line(Guid.NewGuid(), 0.333m, 0.10m).LineTotal);
        Assert.Equal(0m, Line(Guid.NewGuid(), 1m, 0m).LineTotal);
    }

    [Fact]
    public void Customer_snapshot_has_value_equality_and_no_setters()
    {
        var customer = new CustomerSnapshot(" ACME ", "123", " test@example.com ");
        Assert.Equal(new CustomerSnapshot("ACME", "123", "test@example.com"), customer);
        Assert.All(typeof(CustomerSnapshot).GetProperties(), property => Assert.Null(property.SetMethod));
        Assert.True(typeof(CustomerSnapshot).IsSealed);
    }

    [Fact]
    public void Payment_after_cancellation_records_paid_state_and_is_idempotent()
    {
        var order = Create([Line(Guid.NewGuid(), 1m, 10m)]);
        var occurredAt = DateTimeOffset.UtcNow;
        order.Cancel(occurredAt);
        order.PayForOrder(occurredAt);
        order.PayForOrder(occurredAt.AddMinutes(1));

        var domainEvent = Assert.Single(order.DomainEvents.OfType<PaidCancelledOrderEvent>());
        Assert.Equal(PaymentStatus.Paid, domainEvent.PaymentStatus);
        Assert.Equal(OrderStatus.Cancelled, domainEvent.OrderStatus);
        Assert.Equal(occurredAt, order.UpdatedAt);
    }

    private static OrderLine Line(Guid id, decimal quantity, decimal price)
        => new(id, "SKU", "Item", quantity, price);

    private static Order Create(List<OrderLine> lines)
        => Order.CreateOrder(Guid.NewGuid(), "portal", "421",
            new CustomerSnapshot("ACME", "123", "test@example.com"),
            "RUB", DateTimeOffset.UtcNow, lines);
}
