using OrderBridge.Integrations.Domain.ValueObjects;

namespace OrderBridge.Integrations.Application.Features.OrderIntegrations.ValueObjects;

public sealed record OrderIntegrationsFilter(
    Guid? Id = null,
    Guid? OrderId = null,
    IntegrationTarget? IntegrationTarget = null,
    IntegrationOperation? IntegrationOperation = null,
    IntegrationStatus? IntegrationStatus = null,
    int? AttemptCountFrom = null,
    int? AttemptCountTo = null,
    DateTimeOffset? CreatedAtFrom = null,
    DateTimeOffset? CreatedAtTo = null,
    DateTimeOffset? NextAttemptAtTo = null,
    string? SearchTerm = null);
