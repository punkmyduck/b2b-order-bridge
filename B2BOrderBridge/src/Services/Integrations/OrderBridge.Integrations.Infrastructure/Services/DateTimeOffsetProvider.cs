using Shared.Application.Services;

namespace OrderBridge.Integrations.Infrastructure.Services;

public class DateTimeOffsetProvider : IDateTimeOffsetProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
