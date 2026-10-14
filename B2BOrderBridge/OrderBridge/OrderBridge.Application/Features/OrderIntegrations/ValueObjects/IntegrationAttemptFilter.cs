namespace OrderBridge.Application.Features.OrderIntegrations.ValueObjects;

public record IntegrationAttemptFilter(
    Guid? Id = null,
    Guid? OrderIntegrationId = null,
    int? AttemptNumberFrom = null,
    int? AttemptNumberTo = null,
    IntegrationAttemptOutcome? Outcome = null,
    int? ResponseStatusCode = null,
    string? ErrorCodeSearchTerm = null,
    string? ErrorMessageSearchTerm = null,
    string? TraceIdSearchTerm = null,
    DateTimeOffset? StartedAtFrom = null,
    DateTimeOffset? StartedAtTo = null,
    DateTimeOffset? CompletedAtFrom = null,
    DateTimeOffset? CompletedAtTo = null,
    string? SearchTerm = null);
