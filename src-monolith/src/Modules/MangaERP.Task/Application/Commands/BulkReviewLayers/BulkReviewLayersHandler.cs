using MangaERP.Chapter.Application.Ports;
using ChapterEntity = MangaERP.Chapter.Domain.Entities.Chapter;
using PreviewPageEntity = MangaERP.Chapter.Domain.Entities.PreviewPage;
using PageTaskEntity = MangaERP.Chapter.Domain.Entities.PageTask;
using MangaSeries = MangaERP.Series.Domain.Entities.MangaSeries;
using MangaERP.Shared.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using MediatR;

namespace MangaERP.Task.Application.Commands.BulkReviewLayers;

public class BulkReviewLayersHandler : IRequestHandler<BulkReviewLayersCommand, BulkReviewLayersResult>
{
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IArtworkLayerRepository _layerRepo;
    private readonly IPreviewPageRepository _previewRepo;
    private readonly INotificationService _notificationService;

    public BulkReviewLayersHandler(
        IPageTaskRepository pageTaskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IArtworkLayerRepository layerRepo,
        IPreviewPageRepository previewRepo,
        INotificationService notificationService)
    {
        _pageTaskRepo = pageTaskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _layerRepo = layerRepo;
        _previewRepo = previewRepo;
        _notificationService = notificationService;
    }

    public async Task<BulkReviewLayersResult> Handle(BulkReviewLayersCommand cmd, CancellationToken ct)
    {
        var results = new List<BulkPageReviewResult>();
        
        // Cache verified chapters and series to optimize performance
        var verifiedChapters = new Dictionary<Guid, (ChapterEntity chapter, MangaSeries series)>();

        foreach (var review in cmd.Reviews)
        {
            var pageTask = await _pageTaskRepo.GetByIdAsync(review.PageTaskId, ct)
                ?? throw new KeyNotFoundException($"Page task {review.PageTaskId} not found.");

            if (!verifiedChapters.TryGetValue(pageTask.ChapterId, out var cached))
            {
                var chapter = await _chapterRepo.GetByIdAsync(pageTask.ChapterId, ct)
                    ?? throw new KeyNotFoundException($"Chapter {pageTask.ChapterId} not found.");

                var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
                    ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

                chapter.EnsureOwnedBy(cmd.MangakaId, series.AuthorId);
                cached = (chapter, series);
                verifiedChapters.Add(pageTask.ChapterId, cached);
            }

            var layer = await _layerRepo.GetCurrentByPageTaskIdAsync(review.PageTaskId, ct)
                ?? throw new InvalidOperationException($"No artwork layer is pending review for PageTask {review.PageTaskId}.");

            string? previewUrl = null;

            if (review.IsAccepted)
            {
                layer.MarkAccepted();
                pageTask.Accept();

                previewUrl = layer.GetDisplayUrl();
                var existingPreview = await _previewRepo.GetByPageTaskIdAsync(pageTask.Id, ct);
                if (existingPreview is null)
                {
                    await _previewRepo.AddAsync(PreviewPageEntity.CreateStub(pageTask.Id, previewUrl), ct);
                }
                else
                {
                    existingPreview.UpdateComposite(previewUrl);
                    await _previewRepo.UpdateAsync(existingPreview, ct);
                }

                if (pageTask.AssignedAssistantId.HasValue)
                {
                    await _notificationService.NotifyTaskApprovedAsync(
                        pageTask.AssignedAssistantId.Value, pageTask.Id, ct);
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(review.RejectionNote))
                    throw new InvalidOperationException($"RejectionNote is required when rejecting layer for PageTask {review.PageTaskId}.");

                layer.MarkRejected(review.RejectionNote);
                pageTask.RequestRevision();

                if (pageTask.AssignedAssistantId.HasValue)
                {
                    await _notificationService.NotifyRevisionRequiredAsync(
                        pageTask.AssignedAssistantId.Value, pageTask.Id, review.RejectionNote, ct);
                }
            }

            await _layerRepo.UpdateAsync(layer, ct);
            await _pageTaskRepo.UpdateAsync(pageTask, ct);

            results.Add(new BulkPageReviewResult(pageTask.Id, pageTask.TaskStatus.ToString(), previewUrl));
        }

        await _layerRepo.SaveChangesAsync(ct);

        return new BulkReviewLayersResult(results);
    }
}
