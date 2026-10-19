using OrderBridge.Integrations.Domain.ValueObjects;

namespace OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;

public sealed record OrderIntegrationDto(
    Guid Id,
    Guid OrderId,
    IntegrationTarget IntegrationTarget,
    IntegrationOperation IntegrationOperation,
    IntegrationStatus IntegrationStatus,
    string? ExternalEntityId,
    string IdempotencyKey,
    int AttemptCount,
    DateTimeOffset? NextAttemptAt,
    string? LastErrorCode,
    string? LastErrorMessage,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastAttemptAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? UpdatedAt);
