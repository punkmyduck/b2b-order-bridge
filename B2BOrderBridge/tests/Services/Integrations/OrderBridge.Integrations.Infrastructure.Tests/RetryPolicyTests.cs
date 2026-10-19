using Xunit;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Services.Implementations;
namespace OrderBridge.Integrations.Infrastructure.Tests;
public sealed class RetryPolicyTests
{
    [Theory]
    [InlineData(1, true)]
    [InlineData(BaseOrderIntegrationRetryPolicy.MaxAttemptsCount - 1, true)]
    [InlineData(BaseOrderIntegrationRetryPolicy.MaxAttemptsCount, false)]
    public void Retry_policy_stops_at_the_maximum_attempt_count(int attemptCount, bool expected)
    {
        var policy = new BaseOrderIntegrationRetryPolicy();

        Assert.Equal(expected, policy.CanRetry(attemptCount));
    }

    [Fact]
    public void Retry_policy_respects_a_longer_retry_after_delay()
    {
        var policy = new BaseOrderIntegrationRetryPolicy();
        var now = new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);

        Assert.Equal(
            now.AddMinutes(2),
            policy.GetNextAttemptAt(1, now, TimeSpan.FromMinutes(2)));
        Assert.Throws<InvalidOperationException>(() =>
            policy.GetNextAttemptAt(BaseOrderIntegrationRetryPolicy.MaxAttemptsCount, now, null));
    }

}
