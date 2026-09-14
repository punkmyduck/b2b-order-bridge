using Shared.Application.Results;

namespace Shared.Application.Messaging;

public interface ICommandHandler<TCommand> : MediatR.IRequestHandler<TCommand, Result>
    where TCommand : ICommand;

public interface ICommandHandler<TCommand, TResponse>
    : MediatR.IRequestHandler<TCommand, Result<TResponse>>
    where TCommand : ICommand<TResponse>;


