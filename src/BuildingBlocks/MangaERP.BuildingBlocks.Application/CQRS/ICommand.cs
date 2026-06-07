using MediatR;

namespace MangaERP.BuildingBlocks.Application.CQRS;

/// <summary>
/// Marker interface for all commands. Commands mutate state and return a Result.
/// </summary>
public interface ICommand<TResult> : IRequest<Result<TResult>> { }

/// <summary>
/// Marker interface for commands that return no data.
/// </summary>
public interface ICommand : IRequest<Result> { }
