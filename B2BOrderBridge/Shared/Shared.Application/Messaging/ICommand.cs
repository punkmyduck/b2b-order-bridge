using Shared.Application.Results;

namespace Shared.Application.Messaging;

public interface ICommand : MediatR.IRequest<Result>;
public interface ICommand<TResponse> : MediatR.IRequest<Result<TResponse>>;


