using Shared.Application.Services;

namespace OrderBridge.Orders.Infrastructure.Services;

public class DateTimeOffsetProvider : IDateTimeOffsetProvider
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
