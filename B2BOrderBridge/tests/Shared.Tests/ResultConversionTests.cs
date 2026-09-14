using Shared.Application.Results;
using Xunit;

namespace Shared.Tests;

public sealed class ResultConversionTests
{
    [Fact]
    public void Value_converts_to_success()
    {
        Result<int> result = 42;

        Assert.True(result.IsSuccess);
        Assert.Null(result.Error);
        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Error_converts_to_both_failure_types()
    {
        var error = new Error("order.not_found", "Order not found.", ErrorType.NotFound);
        Result result = error;
        Result<Guid> typedResult = error;

        Assert.True(result.IsFailure);
        Assert.True(typedResult.IsFailure);
        Assert.Same(error, result.Error);
        Assert.Same(error, typedResult.Error);
        Assert.Throws<InvalidOperationException>(() => typedResult.Value);
    }

    [Fact]
    public void Null_error_cannot_become_success()
    {
        Error error = null!;

        Assert.Throws<ArgumentNullException>(() => { Result result = error; });
        Assert.Throws<ArgumentNullException>(() => { Result<int> result = error; });
    }

    [Fact]
    public void Typed_nullable_value_converts_to_success()
    {
        string? value = null;
        Result<string?> result = value;

        Assert.True(result.IsSuccess);
        Assert.Null(result.Value);
    }

    [Fact]
    public async Task Async_handler_can_return_value_or_error_directly()
    {
        var success = await Handle(true);
        var failure = await Handle(false);

        Assert.Equal(42, success.Value);
        Assert.True(failure.IsFailure);
        Assert.Equal("order.not_found", failure.Error!.Code);
    }

    private static async Task<Result<int>> Handle(bool exists)
    {
        var value = await Task.FromResult(42);

        if (!exists)
            return new Error("order.not_found", "Order not found.", ErrorType.NotFound);

        return value;
    }
}

