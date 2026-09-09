using OrderBridge.Domain.Exceptions;
using OrderBridge.Domain.ValueObjects;
using Shared.Domain;

namespace OrderBridge.Domain.Models;

public class OrderIntegration : AggregateRoot<Guid>
{
    public const int ExternalEntityIdMaxLength = 512;
    public const int IdempotencyKeyMaxLength = 512;
    public const int LastErrorCodeMaxLength = 128;
    public const int LastErrorMessageMaxLength = 1024;


    public Guid OrderId { get; private set; }
    public IntegrationTarget IntegrationTarget { get; private set; }
    public IntegrationOperation IntegrationOperation { get; private set; }
    public IntegrationStatus IntegrationStatus { get; private set; }
    public string? ExternalEntityId { get; private set; }
    public string IdempotencyKey { get; private set; } = null!;

    public int AttemptCount { get; private set; }
    public DateTimeOffset? NextAttemptAt { get; private set; }
    
    public string? LastErrorCode { get; private set; }
    public string? LastErrorMessage { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? LastAttemptAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private OrderIntegration() { }

    private OrderIntegration(
        Guid id,
        Guid orderId,
        IntegrationTarget integrationTarget,
        IntegrationOperation integrationOperation,
        IntegrationStatus integrationStatus,
        string idempotencyKey,
        int attemptCount,
        DateTimeOffset createdAt,
        string? externalEntityId = null,
        DateTimeOffset? nextAttemptAt = null,
        string? lastErrorCode = null,
        string? lastErrorMessage = null,
        DateTimeOffset? lastAttemptAt = null,
        DateTimeOffset? completedAt = null,
        DateTimeOffset? updatedAt = null)
    {
        Id = id;
        OrderId = orderId;
        IntegrationTarget = integrationTarget;
        IntegrationOperation = integrationOperation;
        IntegrationStatus = integrationStatus;
        ExternalEntityId = externalEntityId;
        IdempotencyKey = idempotencyKey;
        AttemptCount = attemptCount;
        CreatedAt = createdAt;
        NextAttemptAt = nextAttemptAt;
        LastErrorCode = lastErrorCode;
        LastErrorMessage = lastErrorMessage;
        LastAttemptAt = lastAttemptAt;
        CompletedAt = completedAt;
        UpdatedAt = updatedAt;
    }

    public static OrderIntegration Create(
        Guid id,
        Order order,
        IntegrationTarget integrationTarget,
        IntegrationOperation integrationOperation,
        string idempotencyKey,
        DateTimeOffset createdAt,
        bool isWaitingForPrerequisite = true)
    {
        Guard.NotNull(order);
        Guard.NotEmpty(order.Id, nameof(OrderId));
        Guard.DefinedEnum(integrationTarget);
        Guard.DefinedEnum(integrationOperation);

        var expectedTarget = integrationOperation switch
        {
            IntegrationOperation.CreateDeal => IntegrationTarget.Bitrix24,
            IntegrationOperation.CreateSalesOrder => IntegrationTarget.OneC,
            IntegrationOperation.CreateDocument => IntegrationTarget.Diadoc,
            IntegrationOperation.SendOrderReceivedNotification => IntegrationTarget.Telegram,
            _ => throw new DomainValidationException(
                "OrderIntegration.Operation.Unsupported", "The integration operation is not supported.")
        };

        if (integrationTarget != expectedTarget)
            throw new DomainValidationException(
                "OrderIntegration.Operation.TargetMismatch",
                "The integration operation is not supported by the selected target.");

        id = Guard.NotEmpty(id, nameof(Id));
        idempotencyKey = Guard.RequiredString(idempotencyKey, IdempotencyKeyMaxLength, parameterName: nameof(IdempotencyKey));

        return new OrderIntegration(
            id,
            order.Id,
            integrationTarget,
            integrationOperation,
            isWaitingForPrerequisite ? IntegrationStatus.WaitingForPrerequisite : IntegrationStatus.Pending,
            idempotencyKey,
            0,
            createdAt);
    }

    public void Release(DateTimeOffset markedAt)
    {
        if (IntegrationStatus != IntegrationStatus.WaitingForPrerequisite)
        {
            throw new DomainInvalidOperationException(
                "OrderIntegration.IntegrationStatus.ReleaseInvalidStatus", 
                "Only an integration waiting for a prerequisite can be released.");
        }

        UpdatedAt = markedAt;
        IntegrationStatus = IntegrationStatus.Pending;
    }

    public void StartAttempt(DateTimeOffset startedAt)
    {
        if (IntegrationStatus != IntegrationStatus.Pending && IntegrationStatus != IntegrationStatus.RetryScheduled)
        {
            throw new DomainInvalidOperationException(
                "OrderIntegration.IntegrationStatus.StartAttemptInvalidStatus", 
                "An attempt can start only when the integration is pending or scheduled for retry.");
        }

        if (IntegrationStatus == IntegrationStatus.RetryScheduled
            && (NextAttemptAt is not { } retryAt || startedAt < retryAt))
            throw new DomainInvalidOperationException(
                "OrderIntegration.Retry.NotDue", "The retry cannot start before its scheduled time.");

        UpdatedAt = startedAt;
        IntegrationStatus = IntegrationStatus.Processing;
        LastAttemptAt = startedAt;
        AttemptCount++;

        NextAttemptAt = null;
    }

    public void MarkSucceeded(string externalId, DateTimeOffset markedAt)
    {
        if (IntegrationStatus != IntegrationStatus.Processing)
        {
            throw new DomainInvalidOperationException(
                "OrderIntegration.IntegrationStatus.MarkSucceededInvalidStatus",
                "Only a processing integration can be marked as succeeded.");
        }

        ExternalEntityId = Guard.RequiredString(externalId, ExternalEntityIdMaxLength, parameterName: nameof(ExternalEntityId));

        UpdatedAt = markedAt;
        IntegrationStatus = IntegrationStatus.Succeeded;
        CompletedAt = markedAt;

        NextAttemptAt = null;
        LastErrorCode = null;
        LastErrorMessage = null;
    }

    public void ScheduleRetry(string errorCode, string message, DateTimeOffset retryAt, DateTimeOffset scheduledAt)
    {
        if (IntegrationStatus != IntegrationStatus.Processing)
        {
            throw new DomainInvalidOperationException(
                "OrderIntegration.IntegrationStatus.ScheduleRetryInvalidStatus",
                "A retry can be scheduled only for a processing integration.");
        }

        var validatedCode = Guard.RequiredString(errorCode, LastErrorCodeMaxLength, parameterName: nameof(LastErrorCode));
        var validatedMessage = Guard.RequiredString(message, LastErrorMessageMaxLength, parameterName: nameof(LastErrorMessage));

        if (retryAt <= scheduledAt)
            throw new DomainValidationException(
                "OrderIntegration.Retry.InvalidTime", "The retry time must be later than the scheduling time.");

        LastErrorCode = validatedCode;
        LastErrorMessage = validatedMessage;
        UpdatedAt = scheduledAt;
        NextAttemptAt = retryAt;
        IntegrationStatus = IntegrationStatus.RetryScheduled;
    }

    public void MarkFailed(string errorCode, string message, DateTimeOffset markedAt)
    {
        if (IntegrationStatus != IntegrationStatus.Processing)
        {
            throw new DomainInvalidOperationException(
                "OrderIntegration.IntegrationStatus.MarkFailedInvalidStatus",
                "Only a processing integration can be marked as failed.");
        }

        var validatedCode = Guard.RequiredString(errorCode, LastErrorCodeMaxLength, parameterName: nameof(LastErrorCode));
        var validatedMessage = Guard.RequiredString(message, LastErrorMessageMaxLength, parameterName: nameof(LastErrorMessage));

        UpdatedAt = markedAt;
        LastErrorCode = validatedCode;
        LastErrorMessage = validatedMessage;
        IntegrationStatus = IntegrationStatus.Failed;
        CompletedAt = markedAt;
        NextAttemptAt = null;
    }

    public void RequestManualRetry(DateTimeOffset requestedAt)
    {
        if (IntegrationStatus != IntegrationStatus.Failed)
        {
            throw new DomainInvalidOperationException(
                "OrderIntegration.IntegrationStatus.RequestManualRetryInvalidStatus",
                "A manual retry can be requested only for a failed integration.");
        }

        CompletedAt = null;
        NextAttemptAt = null;
        UpdatedAt = requestedAt;
        IntegrationStatus = IntegrationStatus.Pending;
    }

    public void Cancel(DateTimeOffset canceledAt)
    {
        if (IntegrationStatus != IntegrationStatus.WaitingForPrerequisite && IntegrationStatus != IntegrationStatus.Pending && IntegrationStatus != IntegrationStatus.RetryScheduled)
        {
            throw new DomainInvalidOperationException(
                "OrderIntegration.IntegrationStatus.CancelInvalidStatus",
                "Only a waiting, pending, or retry-scheduled integration can be cancelled.");
        }

        UpdatedAt = canceledAt;
        IntegrationStatus = IntegrationStatus.Cancelled;
        CompletedAt = canceledAt;
        NextAttemptAt = null;
    }
}

