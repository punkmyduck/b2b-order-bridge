using Mediator;
using Microsoft.Extensions.DependencyInjection;
using Shared.Application.Pagination;
using Shared.Application.Results;
using Shared.Domain;
using Xunit;

namespace Shared.Tests;

public sealed class AbstractionTests
{
    [Fact]
    public void Failed_result_preserves_error_and_rejects_value_access()
    {
        var error = new Error("order.not_found", "Order not found.", ErrorType.NotFound);
        var result = Result<Guid>.Failure(error);

        Assert.True(result.IsFailure);
        Assert.Same(error, result.Error);
        Assert.Throws<InvalidOperationException>(() => result.Value);
        Assert.Throws<ArgumentNullException>(() => Result.Failure(null!));
    }

    [Theory]
    [InlineData(0, 20)]
    [InlineData(1, 0)]
    [InlineData(1, 101)]
    [InlineData(int.MaxValue, 100)]
    public void Invalid_pagination_is_rejected(int pageNumber, int pageSize)
        => Assert.Throws<ArgumentOutOfRangeException>(
            () => new PaginationRequest(pageNumber, pageSize));

    [Fact]
    public void Pagination_copies_items_and_handles_partial_last_page()
    {
        var source = new List<int> { 21 };
        var result = new PaginationResult<int>(source, 21, new(2, 20));
        source.Clear();

        Assert.Single(result.Items);
        Assert.Equal(2, result.TotalPages);
        Assert.True(result.HasPreviousPage);
        Assert.False(result.HasNextPage);
    }

    [Fact]
    public void Pagination_supports_empty_and_out_of_range_pages_and_large_totals()
    {
        var empty = new PaginationResult<int>([], 0, new());
        var beyondLast = new PaginationResult<int>([], 10, new(2, 20));
        var large = new PaginationResult<int>([], long.MaxValue, new(1, 100));

        Assert.Equal(0, empty.TotalPages);
        Assert.False(empty.HasPreviousPage);
        Assert.False(empty.HasNextPage);
        Assert.True(beyondLast.HasPreviousPage);
        Assert.False(beyondLast.HasNextPage);
        Assert.Equal(long.MaxValue / 100 + 1, large.TotalPages);
    }

    [Fact]
    public void Pagination_rejects_inconsistent_item_count()
    {
        Assert.Throws<ArgumentException>(() => new PaginationResult<int>([1, 2], 1, new()));
        Assert.Throws<ArgumentException>(() => new PaginationResult<int>([1], 10, new(2, 20)));
    }

    [Fact]
    public void Aggregate_retains_events_until_explicitly_cleared()
    {
        var aggregate = new SampleAggregate(Guid.NewGuid());
        var domainEvent = new SampleEvent(Guid.NewGuid(), DateTimeOffset.UtcNow);
        aggregate.Raise(domainEvent);

        Assert.Same(domainEvent, Assert.Single(aggregate.DomainEvents));
        Assert.Single(aggregate.DomainEvents);
        aggregate.ClearDomainEvents();
        Assert.Empty(aggregate.DomainEvents);
    }

    [Fact]
    public async Task Mediator_dispatches_shared_cqrs_contracts_and_isolates_scopes()
    {
        using var provider = CreateServices();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var mediator = first.ServiceProvider.GetRequiredService<IMediator>();

        Assert.Same(mediator, first.ServiceProvider.GetRequiredService<IMediator>());
        Assert.NotSame(mediator, second.ServiceProvider.GetRequiredService<IMediator>());
        Assert.True((await mediator.Send(new SetValue(42))).IsSuccess);
        Assert.Equal(42, (await mediator.Send(new GetValue())).Value);
        Assert.Equal(7, (await mediator.Send(new Echo(7))).Value);
        Assert.Equal(0, (await second.ServiceProvider.GetRequiredService<IMediator>()
            .Send(new GetValue())).Value);
    }

    [Fact]
    public async Task Mediator_passes_cancellation_to_handler()
    {
        using var provider = CreateServices();
        using var scope = provider.CreateScope();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
            await scope.ServiceProvider.GetRequiredService<IMediator>()
                .Send(new SetValue(1), cancellation.Token));
    }

    private static ServiceProvider CreateServices()
    {
        var services = new ServiceCollection();
        services.AddScoped<SampleState>();
        services.AddMediator((MediatorOptions options) =>
        {
            options.ServiceLifetime = ServiceLifetime.Scoped;
            options.Assemblies = [typeof(SetValue)];
        });
        return services.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateScopes = true,
            ValidateOnBuild = true
        });
    }

    private sealed class SampleAggregate(Guid id) : AggregateRoot<Guid>(id)
    {
        public void Raise(IDomainEvent domainEvent) => RaiseDomainEvent(domainEvent);
    }

    private sealed record SampleEvent(Guid EventId, DateTimeOffset OccurredAt) : IDomainEvent;
}

public sealed class SampleState
{
    public int Value { get; set; }
}

public sealed record SetValue(int Value) : Shared.Application.Messaging.ICommand;

public sealed class SetValueHandler(SampleState state)
    : Shared.Application.Messaging.ICommandHandler<SetValue>
{
    public ValueTask<Result> Handle(SetValue command, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        state.Value = command.Value;
        return ValueTask.FromResult(Result.Success());
    }
}

public sealed record GetValue : Shared.Application.Messaging.IQuery<int>;

public sealed class GetValueHandler(SampleState state)
    : Shared.Application.Messaging.IQueryHandler<GetValue, int>
{
    public ValueTask<Result<int>> Handle(GetValue query, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result<int>.Success(state.Value));
}

public sealed record Echo(int Value) : Shared.Application.Messaging.ICommand<int>;

public sealed class EchoHandler : Shared.Application.Messaging.ICommandHandler<Echo, int>
{
    public ValueTask<Result<int>> Handle(Echo command, CancellationToken cancellationToken)
        => ValueTask.FromResult(Result<int>.Success(command.Value));
}
