using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.BuildingBlocks.Contracts.IntegrationEvents;
using MangaERP.BuildingBlocks.Infrastructure.Messaging;

namespace MangaERP.Chapter.Application.Commands.SubmitChapterForQA;

/// <summary>
/// MF2 Step 10: Mangaka submits completed chapter for editorial QA.
/// Guards that all pages are approved before allowing transition.
/// </summary>
public record SubmitChapterForQACommand(Guid ChapterId, Guid RequestingMangakaId) : IRequest;

public class SubmitChapterForQAHandler : IRequestHandler<SubmitChapterForQACommand>
{
    private readonly IChapterRepository _chapterRepository;
    private readonly IPageTaskRepository _pageTaskRepository;
    private readonly IEventBus _eventBus;

    public SubmitChapterForQAHandler(
        IChapterRepository chapterRepository,
        IPageTaskRepository pageTaskRepository,
        IEventBus eventBus)
    {
        _chapterRepository = chapterRepository;
        _pageTaskRepository = pageTaskRepository;
        _eventBus = eventBus;
    }

    public async Task Handle(SubmitChapterForQACommand request, CancellationToken cancellationToken)
    {
        var chapter = await _chapterRepository.GetByIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");

        var approvedCount = await _pageTaskRepository.CountApprovedPagesAsync(request.ChapterId, cancellationToken);
        if (approvedCount < chapter.TotalPages)
            throw new InvalidOperationException(
                $"Cannot submit: only {approvedCount} of {chapter.TotalPages} pages are approved.");

        chapter.SubmitForQA();
        await _chapterRepository.UpdateAsync(chapter, cancellationToken);

        var evt = new ChapterSubmittedForQAEvent(
            Guid.NewGuid(), DateTime.UtcNow,
            chapter.Id, chapter.SeriesId, chapter.AssignedEditorId);
        await _eventBus.PublishAsync(evt, cancellationToken);
    }
}
