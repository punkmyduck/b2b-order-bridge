using Shared.Domain;
using Xunit;

namespace Shared.Tests;

public sealed class GuardTests
{
    [Fact]
    public void Strings_are_trimmed_before_length_validation()
    {
        Assert.Equal("ACME", Guard.RequiredString("  ACME \t", 4));
        Assert.Equal("a  b", Guard.RequiredString(" a  b ", 4));
        Assert.Equal("mail", Guard.OptionalString(" mail ", 4));
        Assert.Throws<DomainException>(() => Guard.RequiredString(" a ", 4, 2));
        Assert.Throws<DomainException>(() => Guard.OptionalString(" abcde ", 4));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \t\r\n")]
    public void Blank_strings_are_rejected_or_normalized_to_null(string? value)
    {
        Assert.Equal("guard.required",
            Assert.Throws<DomainException>(() => Guard.RequiredString(value, 100)).Code);
        Assert.Null(Guard.OptionalString(value, 100));
    }

    [Fact]
    public void Money_preserves_amount_and_rejects_silent_rounding()
    {
        Assert.Equal(12.30m, Guard.Money(12.30m));
        Assert.Equal(12.300m, Guard.Money(12.300m));
        Assert.Equal(0m, Guard.Money(0m));
        Assert.Equal(1.234m, Guard.Money(1.234m, decimalPlaces: 3));
        Assert.Equal(decimal.MaxValue, Guard.Money(decimal.MaxValue));
        Assert.Equal("guard.money_precision",
            Assert.Throws<DomainException>(() => Guard.Money(1.234m)).Code);
        Assert.Throws<DomainException>(() => Guard.Money(-1m));
        Assert.Throws<DomainException>(() => Guard.Money(0m, allowZero: false));
        Assert.Throws<DomainException>(() => Guard.Money(1.5m, decimalPlaces: 0));
    }

    [Fact]
    public void Numbers_validate_boundaries_and_non_finite_values()
    {
        Assert.Equal(0.5m, Guard.Positive(0.5m));
        Assert.Equal(0, Guard.NonNegative(0));
        Assert.Equal(1, Guard.InRange(1, 1, 10));
        Assert.Equal(10, Guard.InRange(10, 1, 10));
        Assert.Throws<DomainException>(() => Guard.Positive(0));
        Assert.Throws<DomainException>(() => Guard.NonNegative(-1));
        Assert.Throws<DomainException>(() => Guard.InRange(11, 1, 10));
        Assert.Throws<DomainException>(() => Guard.Positive(double.NaN));
        Assert.Throws<DomainException>(() => Guard.NonNegative(double.PositiveInfinity));
        Assert.Throws<DomainException>(() => Guard.InRange(double.NaN, 0d, 1d));
    }

    [Fact]
    public void Invalid_configuration_is_a_programming_error()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.RequiredString("x", 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.OptionalString(null, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Money(1m, decimalPlaces: 29));
        Assert.Throws<ArgumentOutOfRangeException>(() => Guard.Money(1m, decimalPlaces: -1));
        Assert.Throws<ArgumentException>(() => Guard.InRange(1, 10, 0));
        Assert.Throws<ArgumentException>(() => Guard.InRange(1d, 0d, double.NaN));
    }

    [Fact]
    public void References_identifiers_and_enums_are_validated()
    {
        var instance = new object();
        var id = Guid.NewGuid();
        Assert.Same(instance, Guard.NotNull(instance));
        Assert.Equal(id, Guard.NotEmpty(id));
        Assert.Equal(DayOfWeek.Monday, Guard.DefinedEnum(DayOfWeek.Monday));
        Assert.Throws<DomainException>(() => Guard.NotNull<object>(null));
        Assert.Throws<DomainException>(() => Guard.NotEmpty(Guid.Empty));
        Assert.Throws<DomainException>(() => Guard.DefinedEnum((DayOfWeek)100));
    }

    [Fact]
    public void Error_identifies_argument_without_including_value()
    {
        var companyName = "sensitive customer data";
        var error = Assert.Throws<DomainException>(() => Guard.RequiredString(companyName, 2));
        Assert.Contains(nameof(companyName), error.Message);
        Assert.DoesNotContain(companyName, error.Message);
    }
}
