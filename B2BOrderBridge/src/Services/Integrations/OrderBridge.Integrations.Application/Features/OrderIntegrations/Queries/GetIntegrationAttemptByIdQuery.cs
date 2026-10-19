using OrderBridge.Integrations.Application.Features.OrderIntegrations.Models;
using OrderBridge.Integrations.Application.Features.OrderIntegrations.Repositories;
using Shared.Application.Messaging;
using Shared.Application.Results;

namespace OrderBridge.Integrations.Application.Features.OrderIntegrations.Queries;

public sealed record GetIntegrationAttemptByIdQuery(Guid Id) : IQuery<IntegrationAttempt>;

public sealed class GetIntegrationAttemptByIdQueryHandler(
    IIntegrationAttemptRepository repository)
    : IQueryHandler<GetIntegrationAttemptByIdQuery, IntegrationAttempt>
{
    public async Task<Result<IntegrationAttempt>> Handle(
        GetIntegrationAttemptByIdQuery query,
        CancellationToken cancellationToken)
    {
        var attempt = await repository.GetByIdAsync(query.Id, cancellationToken);

        return attempt is null
            ? new Error(
                "IntegrationAttempt.NotFound",
                $"Integration attempt '{query.Id}' was not found.",
                ErrorType.NotFound)
            : attempt;
    }
}
