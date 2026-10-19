namespace OrderBridge.Application.ValueObjects;

public abstract record IntegrationExecutionResult
{
    public bool IsSuccess => this is IntegrationSucceeded;

    public int? ResponseStatusCode => this switch
    {
        IntegrationSucceeded result => result.HttpStatusCode,
        IntegrationTransientFailure result => result.HttpStatusCode,
        IntegrationPermanentFailure result => result.HttpStatusCode,
        _ => null
    };

    public string? ErrorCode => this switch
    {
        IntegrationTransientFailure result => result.Code,
        IntegrationPermanentFailure result => result.Code,
        _ => null
    };

    public string? ErrorMessage => this switch
    {
        IntegrationTransientFailure result => result.Message,
        IntegrationPermanentFailure result => result.Message,
        _ => null
    };

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
