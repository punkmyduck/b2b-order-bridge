namespace Shared.Application.Results;

public sealed class Result<T> : Result
{
    private readonly T? _value;

    private Result(T value) : base(null) => _value = value;
    private Result(Error error) : base(error) { }

    public T Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    public static implicit operator Result<T>(T value) => Success(value);

    public static implicit operator Result<T>(Error error) => Failure(error);

    public static Result<T> Success(T value) => new(value);

    public new static Result<T> Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }
}
