using MediatR;

namespace MangaERP.BuildingBlocks.Application.CQRS;

public interface ICommandHandler<TCommand, TResult> : IRequestHandler<TCommand, Result<TResult>>
    where TCommand : ICommand<TResult> { }

public interface ICommandHandler<TCommand> : IRequestHandler<TCommand, Result>
    where TCommand : ICommand { }
