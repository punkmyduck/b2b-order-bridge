using OrderBridge.Domain.Exceptions;
using Shared.Domain;

namespace OrderBridge.Domain.ValueObjects;

/// <summary>The currencies supported by this application, all using two decimal places.</summary>
public static class CurrencyCodes
{
    public const string Rub = "RUB";
    public const string Usd = "USD";
    public const string Eur = "EUR";
    public const string Cny = "CNY";
    public const string Byn = "BYN";
    public const int CodeLength = 3;

    public static IReadOnlyList<string> Supported { get; } =
        Array.AsReadOnly(new[] { Rub, Usd, Eur, Cny, Byn });

    public static string Normalize(string? code)
    {
        var normalized = Guard.RequiredString(
            code, maxLength: CodeLength, minLength: CodeLength,
            parameterName: nameof(code)).ToUpperInvariant();

        return Supported.Contains(normalized)
            ? normalized
            : throw new DomainValidationException(
                "Order.Currency.Unsupported", "Currency is not supported.");
    }
}

