using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.QA.Application.Queries.GetQAQueue;

public record GetQAQueueQuery(Guid EditorId) : IRequest<IEnumerable<QAQueueChapterDto>>;

public record QAQueueChapterDto(
    Guid ChapterId,
    Guid SeriesId,
    string Title,
    decimal ChapterNumber,
    string? CoverImageUrl,
    DateTime SubmittedAt,
    string? SeriesTitle = null,
    string? Status = null
);

public class GetQAQueueHandler : IRequestHandler<GetQAQueueQuery, IEnumerable<QAQueueChapterDto>>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;

    public GetQAQueueHandler(IChapterRepository chapterRepo, ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<IEnumerable<QAQueueChapterDto>> Handle(GetQAQueueQuery request, CancellationToken cancellationToken)
    {
        var chapters = (await _chapterRepo.GetQAQueueAsync(request.EditorId, cancellationToken)).ToList();
        if (!chapters.Any())
            return Enumerable.Empty<QAQueueChapterDto>();

        var seriesIds = chapters.Select(c => c.SeriesId).Distinct().ToList();
        var seriesDict = new Dictionary<Guid, string>();
        foreach (var sId in seriesIds)
        {
            var series = await _seriesRepo.GetByIdAsync(sId, cancellationToken);
            if (series != null)
            {
                seriesDict[sId] = series.Title;
            }
        }

        return chapters.Select(c => new QAQueueChapterDto(
            c.Id,
            c.SeriesId,
            c.Title,
            c.ChapterNumber,
            c.CoverImageUrl,
            c.CreatedAt,
            seriesDict.TryGetValue(c.SeriesId, out var sTitle) ? sTitle : "Unknown series",
            c.Status.ToString()
        )).ToList();
    }
}
