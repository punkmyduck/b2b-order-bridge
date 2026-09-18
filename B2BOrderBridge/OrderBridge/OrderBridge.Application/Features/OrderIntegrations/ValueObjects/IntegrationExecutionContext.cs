namespace OrderBridge.Application.Features.OrderIntegrations.ValueObjects;

public sealed record IntegrationExecutionContext(
    Guid IntegrationId,
    string IdempotencyKey,
    int AttemptNumber,
    OrderExecutionSnapshot Order);
