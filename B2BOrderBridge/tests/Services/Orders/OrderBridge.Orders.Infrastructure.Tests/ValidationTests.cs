using FluentValidation;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using OrderBridge.Orders.Application;
using OrderBridge.Orders.Application.Features.Orders.Commands;
using OrderBridge.Orders.Application.Features.Orders.Queries;
using OrderBridge.Orders.Application.Features.Orders.ValueObjects;
using OrderBridge.Orders.Application.ValueObjects;
using Shared.Application.Behaviors;
using Shared.Application.Pagination;
using Shared.Application.Results;
using Xunit;

namespace OrderBridge.Orders.Infrastructure.Tests;
public sealed class ValidationTests
{
    private static CreateOrderCommand Valid() => new(" portal ", "42",
        new CustomerSnapshotDto("ACME", "123", "test@example.com"), " rub ",
        [new OrderLineDto("SKU", "Item", 0.333m, 0.10m)]);

    [Fact]
    public void AddApplication_registers_handlers_and_validators()
    {
        var services = new ServiceCollection().AddApplication();
        Assert.Contains(services, d => d.ServiceType == typeof(IValidator<CreateOrderCommand>));
        Assert.Contains(services, d => d.ServiceType == typeof(IRequestHandler<CreateOrderCommand, Result<CreateOrderResponse>>));
        Assert.Contains(services, d => d.ServiceType == typeof(IValidator<GetPagedOrdersQuery>));
        Assert.Contains(services, d => d.ServiceType == typeof(IPipelineBehavior<,>));
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
    public async Task Paged_orders_query_rejects_invalid_filter_ranges()
    {
        var query = new GetPagedOrdersQuery(
            new PaginationRequest(),
            new OrdersFilter(
                TotalAmountFrom: 100m,
                TotalAmountTo: 10m,
                CreatedAtFrom: DateTimeOffset.UtcNow,
                CreatedAtTo: DateTimeOffset.UtcNow.AddDays(-1)));

        var result = await new GetPagedOrdersQueryValidator().ValidateAsync(query);

        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("TotalAmountFrom"));
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("CreatedAtFrom"));
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
