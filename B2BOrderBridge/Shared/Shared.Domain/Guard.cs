using System.Numerics;
using System.Runtime.CompilerServices;

namespace Shared.Domain;

/// <summary>Returns normalized, validated values or throws a domain exception.</summary>
public static class Guard
{
    public static string RequiredString(string? value, int maxLength, int minLength = 1,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minLength, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, minLength);
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
            throw Invalid("guard.required", parameterName, "is required.");
        ValidateLength(normalized, minLength, maxLength, parameterName);
        return normalized;
    }

    /// <summary>Null, empty and whitespace-only values normalize to null.</summary>
    public static string? OptionalString(string? value, int maxLength,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxLength, 1);
        var normalized = value?.Trim();
        if (string.IsNullOrEmpty(normalized))
            return null;
        ValidateLength(normalized, 1, maxLength, parameterName);
        return normalized;
    }

    public static T NotNull<T>(T? value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null) where T : class
        => value ?? throw Invalid("guard.required", parameterName, "is required.");

    public static Guid NotEmpty(Guid value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        => value != Guid.Empty ? value
            : throw Invalid("guard.empty_id", parameterName, "must not be an empty identifier.");

    public static T Positive<T>(T value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null) where T : INumber<T>
    {
        if (!T.IsFinite(value) || value <= T.Zero)
            throw Invalid("guard.positive", parameterName, "must be finite and greater than zero.");
        return value;
    }

    public static T NonNegative<T>(T value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null) where T : INumber<T>
    {
        if (!T.IsFinite(value) || value < T.Zero)
            throw Invalid("guard.non_negative", parameterName, "must be finite and non-negative.");
        return value;
    }

    public static T InRange<T>(T value, T minimum, T maximum,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null) where T : INumber<T>
    {
        if (!T.IsFinite(minimum) || !T.IsFinite(maximum) || minimum > maximum)
            throw new ArgumentException("Bounds must be finite and minimum must not exceed maximum.");
        if (!T.IsFinite(value) || value < minimum || value > maximum)
            throw Invalid("guard.out_of_range", parameterName, "is outside the inclusive allowed range.");
        return value;
    }

    /// <summary>Validates a non-negative decimal amount without silently rounding it.</summary>
    public static decimal Money(decimal value, int decimalPlaces = 2, bool allowZero = true,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(decimalPlaces);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(decimalPlaces, 28);
        if (allowZero)
            NonNegative(value, parameterName);
        else
            Positive(value, parameterName);
        if (decimal.Round(value, decimalPlaces) != value)
            throw Invalid("guard.money_precision", parameterName, "has too many fractional digits.");
        return value;
    }

    /// <summary>Accepts declared enum values; combined flags need a separate domain rule.</summary>
    public static T DefinedEnum<T>(T value,
        [CallerArgumentExpression(nameof(value))] string? parameterName = null) where T : struct, Enum
        => Enum.IsDefined(value) ? value
            : throw Invalid("guard.invalid_enum", parameterName, "is not a declared enum value.");

    private static void ValidateLength(string value, int minimum, int maximum, string? parameterName)
    {
        if (value.Length < minimum || value.Length > maximum)
            throw Invalid("guard.string_length", parameterName, "has an invalid length.");
    }

    private static DomainException Invalid(string code, string? parameterName, string message)
        => new(code, $"{parameterName ?? "Value"} {message}");
}
