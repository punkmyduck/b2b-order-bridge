namespace Shared.Application.Services;

public interface IDateTimeOffsetProvider
{
    DateTimeOffset UtcNow { get; }
}
