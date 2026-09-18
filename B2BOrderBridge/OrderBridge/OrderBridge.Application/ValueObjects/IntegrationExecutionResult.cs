namespace OrderBridge.Application.ValueObjects;

public abstract record IntegrationExecutionResult
{
    public sealed record IntegrationSucceeded(
        string ExternalEntityId,
        int? HttpStatusCode) : IntegrationExecutionResult;

    public sealed record IntegrationTransientFailure(
        string Code,
        string Message,
        int? HttpStatusCode,
        TimeSpan? RetryAfter) : IntegrationExecutionResult;

    public sealed record IntegrationPermanentFailure(
        string Code,
        string Message,
        int? HttpStatusCode) : IntegrationExecutionResult;
}
