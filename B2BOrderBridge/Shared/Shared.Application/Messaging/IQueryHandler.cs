using Shared.Application.Results;

namespace Shared.Application.Messaging;

public interface IQueryHandler<TQuery, TResponse>
    : MediatR.IRequestHandler<TQuery, Result<TResponse>>
    where TQuery : IQuery<TResponse>;


