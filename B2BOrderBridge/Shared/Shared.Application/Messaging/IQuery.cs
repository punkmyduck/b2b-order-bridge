using Shared.Application.Results;

namespace Shared.Application.Messaging;

public interface IQuery<TResponse> : Mediator.IQuery<Result<TResponse>>;

