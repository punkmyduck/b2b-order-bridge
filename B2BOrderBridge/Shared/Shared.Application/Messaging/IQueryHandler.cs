using Shared.Application.Results;

namespace Shared.Application.Messaging;

public interface IQueryHandler<TQuery, TResponse>
    : Mediator.IQueryHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;

