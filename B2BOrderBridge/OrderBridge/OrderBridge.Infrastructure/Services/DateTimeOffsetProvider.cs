using Shared.Application.Services;

namespace OrderBridge.Infrastructure.Services;

public class DateTimeOffsetProvider : IDateTimeOffsetProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
