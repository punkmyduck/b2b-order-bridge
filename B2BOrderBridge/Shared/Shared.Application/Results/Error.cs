namespace Shared.Application.Results;

public sealed record Error
{
    public Error(string code, string description, ErrorType type = ErrorType.Failure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(code);
        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        if (!Enum.IsDefined(type))
            throw new ArgumentOutOfRangeException(nameof(type));

        Code = code;
        Description = description;
        Type = type;
    }

    public string Code { get; }
    public string Description { get; }
    public ErrorType Type { get; }
}
