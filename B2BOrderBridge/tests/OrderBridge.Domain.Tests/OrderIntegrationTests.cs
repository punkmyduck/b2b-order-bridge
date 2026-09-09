using OrderBridge.Domain.Exceptions;
using OrderBridge.Domain.Models;
using OrderBridge.Domain.ValueObjects;
using Shared.Domain;
using Xunit;

namespace OrderBridge.Domain.Tests;

public sealed class OrderIntegrationTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 9, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Retry_waits_until_due_and_preserves_identity()
    {
        var task = Create();
        task.StartAttempt(Now);
        task.ScheduleRetry("503", "Unavailable", Now.AddMinutes(1), Now);
        Assert.Throws<DomainInvalidOperationException>(() => task.StartAttempt(Now.AddSeconds(59)));
        Assert.Equal(1, task.AttemptCount);
        Assert.Equal(IntegrationStatus.RetryScheduled, task.IntegrationStatus);

        task.StartAttempt(Now.AddMinutes(1));
        task.MarkSucceeded(" deal-1 ", Now.AddMinutes(2));

        Assert.Equal(2, task.AttemptCount);
        Assert.Equal("key", task.IdempotencyKey);
        Assert.Equal("deal-1", task.ExternalEntityId);
        Assert.Equal(IntegrationStatus.Succeeded, task.IntegrationStatus);
        Assert.Null(task.NextAttemptAt);
        Assert.Null(task.LastErrorCode);
        Assert.Equal(Now.AddMinutes(2), task.CompletedAt);
        Assert.Throws<DomainInvalidOperationException>(() => task.StartAttempt(Now.AddMinutes(3)));
    }

    [Fact]
    public void Manual_retry_clears_completion_but_keeps_error_and_attempt_count()
    {
        var task = Create();
        task.StartAttempt(Now);
        task.MarkFailed("invalid", "Invalid request.", Now);
        Assert.Equal(Now, task.CompletedAt);

        task.RequestManualRetry(Now.AddMinutes(1));
        Assert.Null(task.CompletedAt);
        Assert.Equal("invalid", task.LastErrorCode);
        Assert.Equal(1, task.AttemptCount);
        task.StartAttempt(Now.AddMinutes(1));
        Assert.Equal(2, task.AttemptCount);
        Assert.Equal("key", task.IdempotencyKey);
    }

    [Fact]
    public void Waiting_task_must_be_released_before_start()
    {
        var task = Create(waiting: true);
        Assert.Throws<DomainInvalidOperationException>(() => task.StartAttempt(Now));
        task.Release(Now);
        Assert.Throws<DomainInvalidOperationException>(() => task.Release(Now));
        task.StartAttempt(Now);
        Assert.Throws<DomainInvalidOperationException>(() => task.StartAttempt(Now));
        Assert.Throws<DomainInvalidOperationException>(() => task.Cancel(Now));
    }

    [Theory]
    [InlineData(IntegrationStatus.WaitingForPrerequisite)]
    [InlineData(IntegrationStatus.Pending)]
    [InlineData(IntegrationStatus.RetryScheduled)]
    public void Queued_task_can_be_cancelled(IntegrationStatus status)
    {
        var task = Create(waiting: status == IntegrationStatus.WaitingForPrerequisite);
        if (status == IntegrationStatus.RetryScheduled)
        {
            task.StartAttempt(Now);
            task.ScheduleRetry("503", "Unavailable", Now.AddMinutes(1), Now);
        }

        task.Cancel(Now);
        Assert.Equal(IntegrationStatus.Cancelled, task.IntegrationStatus);
        Assert.Equal(Now, task.CompletedAt);
        Assert.Null(task.NextAttemptAt);
        Assert.Throws<DomainInvalidOperationException>(() => task.StartAttempt(Now));
    }

    [Fact]
    public void Failed_validation_does_not_partially_mutate_state()
    {
        var task = Create();
        task.StartAttempt(Now);
        Assert.Throws<DomainException>(() => task.ScheduleRetry("503", " ", Now.AddMinutes(1), Now));
        Assert.Throws<DomainException>(() => task.MarkFailed("503", " ", Now));
        Assert.Throws<DomainValidationException>(() => task.ScheduleRetry("503", "Unavailable", Now, Now));
        Assert.Null(task.LastErrorCode);
        Assert.Null(task.LastErrorMessage);
        Assert.Null(task.NextAttemptAt);
        Assert.Null(task.CompletedAt);
        Assert.Equal(IntegrationStatus.Processing, task.IntegrationStatus);
    }

    [Fact]
    public void Factory_rejects_invalid_and_mismatched_destinations()
    {
        Assert.Throws<DomainException>(() => Create(target: (IntegrationTarget)999));
        Assert.Throws<DomainException>(() => Create(operation: (IntegrationOperation)999));
        Assert.Throws<DomainValidationException>(() => Create(target: IntegrationTarget.Diadoc));
    }

    private static OrderIntegration Create(bool waiting = false,
        IntegrationTarget target = IntegrationTarget.Bitrix24,
        IntegrationOperation operation = IntegrationOperation.CreateDeal)
    {
        var order = Order.CreateOrder(Guid.NewGuid(), "portal", "421",
            new CustomerSnapshot("ACME", "123", "test@example.com"), "RUB", Now,
            [new OrderLine(Guid.NewGuid(), "SKU", "Item", 1m, 10m)]);
        return OrderIntegration.Create(Guid.NewGuid(), order, target, operation, "key", Now, waiting);
    }
}
