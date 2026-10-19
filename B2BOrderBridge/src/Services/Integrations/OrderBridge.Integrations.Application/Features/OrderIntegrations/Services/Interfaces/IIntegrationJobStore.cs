namespace OrderBridge.Integrations.Application.Features.OrderIntegrations.Services.Interfaces;

public interface IIntegrationJobStore
{
    Task<IReadOnlyList<Guid>> GetReadyIntegrationIdsAsync(DateTimeOffset now, int batchSize, CancellationToken cancellationToken);
}
