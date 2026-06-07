using MediatR;

namespace MangaERP.BuildingBlocks.Application.CQRS;

/// <summary>
/// Marker interface for all queries. Queries do not mutate state.
/// </summary>
public interface IQuery<TResult> : IRequest<Result<TResult>> { }
