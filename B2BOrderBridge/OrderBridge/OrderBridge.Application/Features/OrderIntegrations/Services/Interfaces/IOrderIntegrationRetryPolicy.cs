namespace OrderBridge.Application.Features.OrderIntegrations.Services.Interfaces;

public interface IOrderIntegrationRetryPolicy
{
    DateTimeOffset GetNextAttemptAt(int attemptCount, DateTimeOffset now, TimeSpan? retryAfter);
    bool CanRetry(int attemptCount);
}
