using MediatR;

namespace MangaERP.BuildingBlocks.Application.CQRS;

public interface IQueryHandler<TQuery, TResult> : IRequestHandler<TQuery, Result<TResult>>
    where TQuery : IQuery<TResult> { }
