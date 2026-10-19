using OrderBridge.Application.Features.OrderIntegrations.Services.Interfaces;

namespace OrderBridge.Application.Features.OrderIntegrations.Services.Implementations;

public sealed class BaseOrderIntegrationRetryPolicy : IOrderIntegrationRetryPolicy
{
    public const int MaxAttemptsCount = 10;
    public const int PauseBetweenAttemptsInSeconds = 30;

    public bool CanRetry(int attemptCount)
    {
        return attemptCount < MaxAttemptsCount;
    }

    public DateTimeOffset GetNextAttemptAt(int attemptCount, DateTimeOffset now, TimeSpan? retryAfter)
    {
        if (!CanRetry(attemptCount))
            throw new InvalidOperationException("The maximum number of attempts has been reached.");

        var nextAttemptAt = now.AddSeconds(PauseBetweenAttemptsInSeconds);

        if (retryAfter is not null && now + retryAfter.Value > nextAttemptAt)
            return now + retryAfter.Value;

        return nextAttemptAt;
    }
}
