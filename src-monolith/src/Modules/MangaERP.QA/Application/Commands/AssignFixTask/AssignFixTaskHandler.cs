using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.QA.Application.Ports;

namespace MangaERP.QA.Application.Commands.AssignFixTask;

public record AssignFixTaskCommand(Guid PinId, Guid RequesterId, Guid AssistantId, string? Instructions) : IRequest<bool>;

public class AssignFixTaskHandler : IRequestHandler<AssignFixTaskCommand, bool>
{
    private readonly IBugPinRepository _bugPinRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;

    public AssignFixTaskHandler(
        IBugPinRepository bugPinRepo,
        IPageTaskRepository pageTaskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo)
    {
        _bugPinRepo = bugPinRepo;
        _pageTaskRepo = pageTaskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<bool> Handle(AssignFixTaskCommand request, CancellationToken cancellationToken)
    {
        var pin = await _bugPinRepo.GetByIdAsync(request.PinId, cancellationToken)
            ?? throw new KeyNotFoundException($"BugPin {request.PinId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(pin.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {pin.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, cancellationToken)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        if (series.AuthorId != request.RequesterId)
            throw new UnauthorizedAccessException("Bạn không phải tác giả (Mangaka) của chương truyện này.");

        if (pin.Status != "Open")
            throw new InvalidOperationException("Chỉ có thể giao sửa lỗi cho pin đang ở trạng thái Open.");

        var pageTask = await _pageTaskRepo.GetByIdAsync(pin.PageTaskId, cancellationToken)
            ?? throw new KeyNotFoundException($"PageTask {pin.PageTaskId} not found.");

        // Update BugPin status to InFixing
        pin.Status = "InFixing";
        await _bugPinRepo.UpdateAsync(pin, cancellationToken);

        // Reopen the page task and assign to the assistant
        pageTask.ReopenForFix(request.AssistantId, request.Instructions);
        await _pageTaskRepo.UpdateAsync(pageTask, cancellationToken);
        await _pageTaskRepo.SaveChangesAsync(cancellationToken);

        return true;
    }
}
