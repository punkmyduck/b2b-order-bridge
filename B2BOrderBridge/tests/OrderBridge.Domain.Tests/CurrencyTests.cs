using OrderBridge.Domain.Models;
using OrderBridge.Domain.ValueObjects;
using Shared.Domain;
using Xunit;

namespace OrderBridge.Domain.Tests;

public sealed class CurrencyTests
{
    [Theory]
    [InlineData(" rub ", "RUB")]
    [InlineData("usd", "USD")]
    [InlineData("EuR", "EUR")]
    [InlineData("\tCny\n", "CNY")]
    public void Order_stores_normalized_supported_currency(string input, string expected)
    {
        var order = CreateOrder(input);
        Assert.Equal(expected, order.Currency);
        Assert.Equal(20m, order.TotalAmount);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("RU")]
    [InlineData("RUBL")]
    [InlineData("XXX")]
    [InlineData("JPY")]
    [InlineData("R B")]
    public void Order_rejects_missing_malformed_or_unsupported_currency(string? input)
        => Assert.ThrowsAny<DomainException>(() => CreateOrder(input!));

    [Fact]
    public void Every_advertised_currency_is_accepted()
    {
        foreach (var code in CurrencyCodes.Supported)
            Assert.Equal(code, CurrencyCodes.Normalize(code));
    }

    private static Order CreateOrder(string currency)
        => Order.CreateOrder(
            Guid.NewGuid(), "portal", "421",
            new CustomerSnapshot("ACME", "1234567890", "test@example.com"),
            currency, DateTimeOffset.UtcNow,
            [new OrderLine(Guid.NewGuid(), "SKU-1", "Item", 2m, 10m)]);
}
