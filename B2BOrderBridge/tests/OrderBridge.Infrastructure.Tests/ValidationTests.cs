using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OrderBridge.Application;
using OrderBridge.Application.Features.Orders.Commands;
using OrderBridge.Application.Features.Orders.Queries;
using OrderBridge.Application.Features.Orders.ValueObjects;
using OrderBridge.Application.Services.Interfaces;
using OrderBridge.Application.ValueObjects;
using Shared.Application.Behaviors;
using Shared.Application.Results;
using Xunit;

namespace OrderBridge.Infrastructure.Tests;

public sealed class ValidationTests
{
    private static CreateOrderCommand Valid() => new(" portal ", "42",
        new CustomerSnapshotDto("ACME", "123", "test@example.com"), " rub ",
        [new OrderLineDto("SKU", "Item", 0.333m, 0.10m)]);

    [Fact]
    public void AddApplication_registers_handlers_validators_and_pipeline_behaviours()
    {
        var services = new ServiceCollection();

        services.AddApplication();

        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IRequestHandler<CreateOrderCommand, Result<CreateOrderResponse>>));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IValidator<CreateOrderCommand>));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IRequestHandler<GetOrderByIdQuery, Result<OrderSummaryDto>>));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IValidator<GetOrderByIdQuery>));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IPipelineBehavior<,>) &&
            descriptor.ImplementationType == typeof(ValidationBehaviour<,>));
        Assert.Contains(services, descriptor =>
            descriptor.ServiceType == typeof(IOrderIntegrationPlanner));
    }

    [Fact]
    public async Task Valid_normalizable_input_passes()
        => Assert.True((await new CreateOrderCommandValidator().ValidateAsync(Valid())).IsValid);

    [Fact]
    public async Task Missing_nested_objects_produce_failures_not_exceptions()
    {
        var validator = new CreateOrderCommandValidator();
        var result = await validator.ValidateAsync(Valid() with { Customer = null!, Lines = null! });
        Assert.Contains(result.Errors, x => x.PropertyName == "Customer");
        Assert.Contains(result.Errors, x => x.PropertyName == "Lines");
        Assert.False((await validator.ValidateAsync(Valid() with { Lines = [null!] })).IsValid);
    }

    [Fact]
    public async Task Invalid_amounts_currency_and_trimmed_lengths_fail()
    {
        var command = Valid() with
        {
            SourceSystem = new string('x', 65), Currency = "XXX",
            Lines = [new OrderLineDto("", "", 0m, 1.234m)]
        };
        var result = await new CreateOrderCommandValidator().ValidateAsync(command);
        Assert.Contains(result.Errors, x => x.PropertyName == "Lines[0].UnitPrice");
        Assert.Contains(result.Errors, x => x.PropertyName == "Currency");
        Assert.Contains(result.Errors, x => x.PropertyName == "SourceSystem");
    }

    [Fact]
    public async Task Decimal_overflow_is_a_validation_failure()
    {
        var command = Valid() with { Lines = [new OrderLineDto("SKU", "Item", decimal.MaxValue, 2m)] };
        Assert.False((await new CreateOrderCommandValidator().ValidateAsync(command)).IsValid);
    }

    [Fact]
    public async Task Behaviour_stops_handler_and_preserves_field_errors()
    {
        var behaviour = new ValidationBehaviour<CreateOrderCommand, int>([new CreateOrderCommandValidator()]);
        var called = false;
        var exception = await Assert.ThrowsAsync<ValidationException>(() => behaviour.Handle(
            Valid() with { Currency = "XXX" }, _ => { called = true; return Task.FromResult(42); }, default));
        Assert.False(called);
        Assert.Contains(exception.Errors, x => x.PropertyName == "Currency");
        Assert.Equal(42, await behaviour.Handle(Valid(), _ => Task.FromResult(42), default));
    }

    [Fact]
    public async Task Async_rules_and_cancellation_are_supported()
    {
        var validator = new InlineValidator<string>();
        validator.RuleFor(x => x).MustAsync(async (_, ct) =>
        {
            await Task.Delay(1, ct);
            return false;
        });
        var behaviour = new ValidationBehaviour<string, int>([validator]);
        await Assert.ThrowsAsync<ValidationException>(() => behaviour.Handle("x", _ => Task.FromResult(1), default));
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => behaviour.Handle("x", _ => Task.FromResult(1), cts.Token));
    }
}
