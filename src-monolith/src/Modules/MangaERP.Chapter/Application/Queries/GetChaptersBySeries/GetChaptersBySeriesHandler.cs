using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Chapter.Application.Queries.GetChaptersBySeries;

public record GetChaptersBySeriesQuery(Guid RequesterId, Guid SeriesId) : IRequest<IEnumerable<ChapterListItemDto>>;

public record ChapterListItemDto(
    Guid ChapterId,
    string Title,
    decimal ChapterNumber,
    string Status,
    int TotalPages,
    int ApprovedPages,
    double ProgressPercent);

public class GetChaptersBySeriesHandler : IRequestHandler<GetChaptersBySeriesQuery, IEnumerable<ChapterListItemDto>>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly ISeriesRepository _seriesRepo;

    public GetChaptersBySeriesHandler(
        IChapterRepository chapterRepo,
        IPageTaskRepository pageTaskRepo,
        ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _pageTaskRepo = pageTaskRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<IEnumerable<ChapterListItemDto>> Handle(GetChaptersBySeriesQuery query, CancellationToken ct)
    {
        var series = await _seriesRepo.GetByIdAsync(query.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {query.SeriesId} not found.");

        var chapters = await _chapterRepo.GetBySeriesIdAsync(query.SeriesId, ct);
        var result = new List<ChapterListItemDto>();

        foreach (var chapter in chapters)
        {
            var approved = await _pageTaskRepo.CountApprovedPagesAsync(chapter.Id, ct);
            var progress = chapter.TotalPages > 0
                ? Math.Round(approved * 100.0 / chapter.TotalPages, 1)
                : 0;

            result.Add(new ChapterListItemDto(
                chapter.Id,
                chapter.Title,
                chapter.ChapterNumber,
                chapter.Status.ToString(),
                chapter.TotalPages,
                approved,
                progress));
        }

        return result;
    }
}
