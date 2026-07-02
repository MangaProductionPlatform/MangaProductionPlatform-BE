using MangaERP.Chapter.Application.Ports;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using PageTaskEntity = MangaERP.Chapter.Domain.Entities.PageTask;
using MangaERP.Series.Application.Ports;
using MangaSeries = MangaERP.Series.Domain.Entities.MangaSeries;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using MediatR;

namespace MangaERP.Task.Application.Queries.GetLayerHistory;

public class GetLayerHistoryHandler : IRequestHandler<GetLayerHistoryQuery, IEnumerable<LayerHistoryDto>>
{
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IArtworkLayerRepository _layerRepo;

    public GetLayerHistoryHandler(
        IPageTaskRepository pageTaskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IArtworkLayerRepository layerRepo)
    {
        _pageTaskRepo = pageTaskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _layerRepo = layerRepo;
    }

    public async Task<IEnumerable<LayerHistoryDto>> Handle(GetLayerHistoryQuery query, CancellationToken ct)
    {
        var targetPageTasks = new List<PageTaskEntity>();

        if (query.PageTaskId.HasValue)
        {
            var pageTask = await _pageTaskRepo.GetByIdAsync(query.PageTaskId.Value, ct)
                ?? throw new KeyNotFoundException($"Page task {query.PageTaskId.Value} not found.");

            var chapter = await _chapterRepo.GetByIdAsync(pageTask.ChapterId, ct)
                ?? throw new KeyNotFoundException($"Chapter {pageTask.ChapterId} not found.");

            var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
                ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

            chapter.EnsureOwnedBy(query.MangakaId, series.AuthorId);
            targetPageTasks.Add(pageTask);
        }
        else if (query.ChapterId.HasValue)
        {
            var chapter = await _chapterRepo.GetByIdAsync(query.ChapterId.Value, ct)
                ?? throw new KeyNotFoundException($"Chapter {query.ChapterId.Value} not found.");

            var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
                ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

            chapter.EnsureOwnedBy(query.MangakaId, series.AuthorId);

            var tasks = await _pageTaskRepo.GetByChapterIdAsync(query.ChapterId.Value, ct);
            targetPageTasks.AddRange(tasks);
        }
        else if (query.SeriesId.HasValue)
        {
            var series = await _seriesRepo.GetByIdAsync(query.SeriesId.Value, ct)
                ?? throw new KeyNotFoundException($"Series {query.SeriesId.Value} not found.");

            if (series.AuthorId != query.MangakaId)
                throw new UnauthorizedAccessException("You do not own this series.");

            var chapters = await _chapterRepo.GetBySeriesIdAsync(query.SeriesId.Value, ct);
            foreach (var chapter in chapters)
            {
                var tasks = await _pageTaskRepo.GetByChapterIdAsync(chapter.Id, ct);
                targetPageTasks.AddRange(tasks);
            }
        }
        else
        {
            // Retrieve all series owned by the Mangaka
            var allSeries = await _seriesRepo.GetByAuthorIdAsync(query.MangakaId, ct);
            foreach (var series in allSeries)
            {
                var chapters = await _chapterRepo.GetBySeriesIdAsync(series.Id, ct);
                foreach (var chapter in chapters)
                {
                    var tasks = await _pageTaskRepo.GetByChapterIdAsync(chapter.Id, ct);
                    targetPageTasks.AddRange(tasks);
                }
            }
        }

        var results = new List<LayerHistoryDto>();

        foreach (var task in targetPageTasks)
        {
            var layers = await _layerRepo.GetByPageTaskIdAsync(task.Id, ct);
            foreach (var layer in layers)
            {
                string statusStr = layer.ReviewedAt == null
                    ? "Pending"
                    : (layer.RejectionNote == null ? "Accepted" : "Rejected");

                if (query.Status != null)
                {
                    if (string.Equals(query.Status, "Current", StringComparison.OrdinalIgnoreCase) && !layer.IsCurrentVersion)
                        continue;
                    if (string.Equals(query.Status, "Accepted", StringComparison.OrdinalIgnoreCase) && (layer.ReviewedAt == null || layer.RejectionNote != null))
                        continue;
                    if (string.Equals(query.Status, "Rejected", StringComparison.OrdinalIgnoreCase) && (layer.ReviewedAt == null || layer.RejectionNote == null))
                        continue;
                    if (string.Equals(query.Status, "Pending", StringComparison.OrdinalIgnoreCase) && layer.ReviewedAt != null)
                        continue;
                }
                else
                {
                    if (layer.ReviewedAt == null)
                        continue;
                }

                results.Add(new LayerHistoryDto(
                    layer.Id,
                    layer.PageTaskId,
                    task.PageNumber,
                    layer.LayerType,
                    layer.FileUrlOriginal,
                    layer.FileUrlOptimized,
                    layer.Version,
                    layer.IsCurrentVersion,
                    layer.RejectionNote,
                    layer.SubmittedAt,
                    layer.ReviewedAt,
                    statusStr
                ));
            }
        }

        return results.OrderByDescending(r => r.SubmittedAt ?? DateTime.MinValue);
    }
}
