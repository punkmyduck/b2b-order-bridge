using OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;
using OrderBridge.Integrations.Application.ValueObjects;
using OrderBridge.Integrations.Domain.Models;
using OrderBridge.Integrations.Domain.ValueObjects;

namespace OrderBridge.Integrations.Application.Features.OrderIntegrations.Models;

public sealed class IntegrationAttempt
{
    public const int ErrorCodeMaxLength = 128;
    public const int ErrorMessageMaxLength = 1024;
    public const int TraceIdMaxLength = 32;

    public Guid Id { get; private set; }
    public Guid OrderIntegrationId { get; private set; }

    public int AttemptNumber { get; private set; }
    public IntegrationAttemptOutcome Outcome { get; private set; }

    public int? ResponseStatusCode { get; private set; }
    public string? ErrorCode { get; private set; }
    public string? ErrorMessage { get; private set; }
    public string? TraceId { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private IntegrationAttempt()
    {
    }

    public static IntegrationAttempt Start(
        OrderIntegration integration,
        DateTimeOffset startedAt,
        string? traceId)
    {
        ArgumentNullException.ThrowIfNull(integration);

        if (integration.IntegrationStatus != IntegrationStatus.Processing)
            throw new InvalidOperationException(
                "An integration attempt can be created only after the integration has started processing.");

        var normalizedTraceId = string.IsNullOrWhiteSpace(traceId)
            ? null
            : traceId.Trim();

        if (normalizedTraceId?.Length > TraceIdMaxLength)
            throw new ArgumentException(
                $"Trace ID must not exceed {TraceIdMaxLength} characters.",
                nameof(traceId));

        return new IntegrationAttempt()
        {
            Id = Guid.NewGuid(),
            OrderIntegrationId = integration.Id,
            AttemptNumber = integration.AttemptCount,
            Outcome = IntegrationAttemptOutcome.Processing,
            StartedAt = startedAt,
            TraceId = normalizedTraceId
        };
    }

    public void Complete(
        IntegrationExecutionResult result,
        DateTimeOffset completedAt)
    {
        ArgumentNullException.ThrowIfNull(result);

        if (Outcome != IntegrationAttemptOutcome.Processing)
            throw new InvalidOperationException("A completed integration attempt cannot be completed again.");

        if (completedAt < StartedAt)
            throw new ArgumentOutOfRangeException(
                nameof(completedAt),
                "The completion time cannot be earlier than the start time.");

        Outcome = result.IsSuccess
            ? IntegrationAttemptOutcome.Succeeded
            : IntegrationAttemptOutcome.Failed;

        ResponseStatusCode = result.ResponseStatusCode;
        ErrorCode = result.ErrorCode;
        ErrorMessage = result.ErrorMessage;

        CompletedAt = completedAt;
    }
}
