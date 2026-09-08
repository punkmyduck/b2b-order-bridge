using Shared.Application.Results;

namespace Shared.Application.Messaging;

public interface ICommand : Mediator.ICommand<Result>;
public interface ICommand<TResponse> : Mediator.ICommand<Result<TResponse>>;

