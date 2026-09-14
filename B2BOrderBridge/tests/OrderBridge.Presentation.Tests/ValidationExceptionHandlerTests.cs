using System.Text.Json;
using FluentValidation;
using FluentValidation.Results;
using Microsoft.AspNetCore.Mvc;
using OrderBridge.Presentation.ExceptionHandlers;
using Xunit;

namespace OrderBridge.Presentation.Tests;

public sealed class ValidationExceptionHandlerTests
{
    [Fact]
    public async Task Fluent_validation_errors_become_validation_problem_details()
    {
        var context = new DefaultHttpContext
        {
            TraceIdentifier = "trace-42",
            Response = { Body = new MemoryStream() },
            Request = { Path = "/api/orders" }
        };
        var exception = new ValidationException(
        [
            new ValidationFailure("Currency", "A supported currency code is required."),
            new ValidationFailure("Currency", "A supported currency code is required."),
            new ValidationFailure("OrderLineDtos[0].Quantity", "Quantity must be greater than zero.")
        ]);

        var handled = await new ValidationExceptionHandler()
            .TryHandleAsync(context, exception, CancellationToken.None);

        Assert.True(handled);
        Assert.Equal(StatusCodes.Status400BadRequest, context.Response.StatusCode);
        Assert.Equal("application/problem+json", context.Response.ContentType);
        context.Response.Body.Position = 0;
        var problem = await JsonSerializer.DeserializeAsync<ValidationProblemDetails>(context.Response.Body);
        Assert.NotNull(problem);
        Assert.Equal("/api/orders", problem.Instance);
        Assert.Single(problem.Errors["Currency"]);
        Assert.Equal("trace-42", Assert.IsType<JsonElement>(problem.Extensions["traceId"]).GetString());
        Assert.Equal("validation.failed", Assert.IsType<JsonElement>(problem.Extensions["code"]).GetString());
    }

    [Fact]
    public async Task Unrelated_exception_is_not_handled()
    {
        var handled = await new ValidationExceptionHandler().TryHandleAsync(
            new DefaultHttpContext(), new InvalidOperationException(), CancellationToken.None);

        Assert.False(handled);
    }
}
