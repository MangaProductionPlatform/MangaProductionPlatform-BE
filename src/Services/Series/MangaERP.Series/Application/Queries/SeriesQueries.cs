using MediatR;
using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;

namespace MangaERP.Series.Application.Queries;

// ─── DTOs ────────────────────────────────────────────────────────────────────
public record MangaSeriesDto(
    Guid Id, string Title, string? Description, string? Genre,
    string? CoverImageUrl, string Status, Guid AuthorId, DateTime CreatedAt);

// ─── Get series by author (Mangaka) ──────────────────────────────────────────
public record GetSeriesByAuthorQuery(Guid AuthorId) : IRequest<IEnumerable<MangaSeriesDto>>;

public class GetSeriesByAuthorHandler : IRequestHandler<GetSeriesByAuthorQuery, IEnumerable<MangaSeriesDto>>
{
    private readonly ISeriesRepository _repository;

    public GetSeriesByAuthorHandler(ISeriesRepository repository) => _repository = repository;

    public async Task<IEnumerable<MangaSeriesDto>> Handle(GetSeriesByAuthorQuery request, CancellationToken cancellationToken)
    {
        var series = await _repository.GetByAuthorIdAsync(request.AuthorId, cancellationToken);
        return series.Select(MappingHelper.ToDto);
    }
}

// ─── Get series detail ────────────────────────────────────────────────────────
public record GetSeriesDetailQuery(Guid SeriesId) : IRequest<MangaSeriesDto?>;

public class GetSeriesDetailHandler : IRequestHandler<GetSeriesDetailQuery, MangaSeriesDto?>
{
    private readonly ISeriesRepository _repository;

    public GetSeriesDetailHandler(ISeriesRepository repository) => _repository = repository;

    public async Task<MangaSeriesDto?> Handle(GetSeriesDetailQuery request, CancellationToken cancellationToken)
    {
        var series = await _repository.GetByIdAsync(request.SeriesId, cancellationToken);
        return series is null ? null : MappingHelper.ToDto(series);
    }
}

// ─── Get all active series (ranking board, public) ───────────────────────────
public record GetAllActiveSeriesQuery : IRequest<IEnumerable<MangaSeriesDto>>;

public class GetAllActiveSeriesHandler : IRequestHandler<GetAllActiveSeriesQuery, IEnumerable<MangaSeriesDto>>
{
    private readonly ISeriesRepository _repository;

    public GetAllActiveSeriesHandler(ISeriesRepository repository) => _repository = repository;

    public async Task<IEnumerable<MangaSeriesDto>> Handle(GetAllActiveSeriesQuery request, CancellationToken cancellationToken)
    {
        var series = await _repository.GetAllActiveAsync(cancellationToken);
        return series.Select(MappingHelper.ToDto);
    }
}

// ─── Cancel series command (Editorial Board) ─────────────────────────────────
public record CancelSeriesCommand(Guid SeriesId) : IRequest;

public class CancelSeriesHandler : IRequestHandler<CancelSeriesCommand>
{
    private readonly ISeriesRepository _repository;

    public CancelSeriesHandler(ISeriesRepository repository) => _repository = repository;

    public async Task Handle(CancelSeriesCommand request, CancellationToken cancellationToken)
    {
        var series = await _repository.GetByIdAsync(request.SeriesId, cancellationToken)
            ?? throw new KeyNotFoundException($"Series {request.SeriesId} not found.");
        series.Cancel();
        await _repository.UpdateAsync(series, cancellationToken);
    }
}

// ─── Helper ──────────────────────────────────────────────────────────────────
internal static class MappingHelper
{
    public static MangaSeriesDto ToDto(MangaSeries s) => new(
        s.Id, s.Title, s.Description, s.Genre,
        s.CoverImageUrl, s.Status.ToString(), s.AuthorId, s.CreatedAt);
}
