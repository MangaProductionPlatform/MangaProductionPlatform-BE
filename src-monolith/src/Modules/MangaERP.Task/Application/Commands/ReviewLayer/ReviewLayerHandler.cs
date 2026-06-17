using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.Task.Application.Ports;
using MediatR;

namespace MangaERP.Task.Application.Commands.ReviewLayer;

public record ReviewLayerCommand(
    Guid MangakaId,
    Guid PageTaskId,
    bool IsAccepted,
    string? RejectionNote
) : IRequest<ReviewLayerResult>;

public record ReviewLayerResult(
    Guid PageTaskId,
    string TaskStatus,
    string? PreviewCompositeUrl);

public class ReviewLayerHandler : IRequestHandler<ReviewLayerCommand, ReviewLayerResult>
{
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IArtworkLayerRepository _layerRepo;
    private readonly IPreviewPageRepository _previewRepo;
    private readonly INotificationService _notificationService;

    public ReviewLayerHandler(
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

    public async Task<ReviewLayerResult> Handle(ReviewLayerCommand cmd, CancellationToken ct)
    {
        var pageTask = await _pageTaskRepo.GetByIdAsync(cmd.PageTaskId, ct)
            ?? throw new KeyNotFoundException($"Page task {cmd.PageTaskId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(pageTask.ChapterId, ct)
            ?? throw new KeyNotFoundException($"Chapter {pageTask.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        chapter.EnsureOwnedBy(cmd.MangakaId, series.AuthorId);

        var layer = await _layerRepo.GetCurrentByPageTaskIdAsync(cmd.PageTaskId, ct)
            ?? throw new InvalidOperationException("No artwork layer is pending review.");

        string? previewUrl = null;

        if (cmd.IsAccepted)
        {
            layer.MarkAccepted();
            pageTask.Accept();

            previewUrl = layer.GetDisplayUrl();
            var existingPreview = await _previewRepo.GetByPageTaskIdAsync(pageTask.Id, ct);
            if (existingPreview is null)
            {
                await _previewRepo.AddAsync(PreviewPage.CreateStub(pageTask.Id, previewUrl), ct);
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
            if (string.IsNullOrWhiteSpace(cmd.RejectionNote))
                throw new InvalidOperationException("RejectionNote is required when rejecting a layer.");

            layer.MarkRejected(cmd.RejectionNote);
            pageTask.RequestRevision();

            if (pageTask.AssignedAssistantId.HasValue)
            {
                await _notificationService.NotifyRevisionRequiredAsync(
                    pageTask.AssignedAssistantId.Value, pageTask.Id, cmd.RejectionNote, ct);
            }
        }

        await _layerRepo.UpdateAsync(layer, ct);
        await _pageTaskRepo.UpdateAsync(pageTask, ct);
        await _layerRepo.SaveChangesAsync(ct);

        return new ReviewLayerResult(pageTask.Id, pageTask.TaskStatus.ToString(), previewUrl);
    }
}

public class ReviewLayerValidator : AbstractValidator<ReviewLayerCommand>
{
    public ReviewLayerValidator()
    {
        RuleFor(x => x.MangakaId).NotEmpty();
        RuleFor(x => x.PageTaskId).NotEmpty();
        RuleFor(x => x.RejectionNote)
            .NotEmpty()
            .When(x => !x.IsAccepted)
            .WithMessage("RejectionNote is required when rejecting a layer.");
    }
}
