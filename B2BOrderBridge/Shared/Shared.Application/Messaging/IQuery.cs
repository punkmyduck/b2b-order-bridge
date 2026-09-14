using Shared.Application.Results;

namespace Shared.Application.Messaging;

public interface IQuery<TResponse> : MediatR.IRequest<Result<TResponse>>;


