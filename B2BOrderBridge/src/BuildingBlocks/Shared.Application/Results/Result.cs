namespace Shared.Application.Results;

public class Result
{
    private protected Result(Error? error) => Error = error;

    public bool IsSuccess => Error is null;
    public bool IsFailure => !IsSuccess;
    public Error? Error { get; }

    public static implicit operator Result(Error error) => Failure(error);

    public static Result Success() => new(null);

    public static Result Failure(Error error)
    {
        ArgumentNullException.ThrowIfNull(error);
        return new(error);
    }
}
