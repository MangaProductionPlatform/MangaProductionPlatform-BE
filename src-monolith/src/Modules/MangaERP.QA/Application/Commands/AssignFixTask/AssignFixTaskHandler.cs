using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.QA.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;

namespace MangaERP.QA.Application.Commands.AssignFixTask;

public record AssignFixTaskCommand(Guid PinId, Guid RequesterId, Guid AssistantId, string? Instructions) : IRequest<bool>;

public class AssignFixTaskHandler : IRequestHandler<AssignFixTaskCommand, bool>
{
    private readonly IBugPinRepository _bugPinRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IStudioInvitationRepository _studioInvitationRepo;

    public AssignFixTaskHandler(
        IBugPinRepository bugPinRepo,
        IPageTaskRepository pageTaskRepo,
        IChapterRepository chapterRepo,
        ISeriesRepository seriesRepo,
        IStudioInvitationRepository studioInvitationRepo)
    {
        _bugPinRepo = bugPinRepo;
        _pageTaskRepo = pageTaskRepo;
        _chapterRepo = chapterRepo;
        _seriesRepo = seriesRepo;
        _studioInvitationRepo = studioInvitationRepo;
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

        if (pin.Status != "Open" && pin.Status != "InFixing")
            throw new InvalidOperationException("Chỉ có thể giao sửa lỗi cho pin đang ở trạng thái Open hoặc InFixing.");

        var pageTask = await _pageTaskRepo.GetByIdAsync(pin.PageTaskId, cancellationToken)
            ?? throw new KeyNotFoundException($"PageTask {pin.PageTaskId} not found.");

        var studioInvitations = await _studioInvitationRepo.GetBySeriesIdAsync(series.Id, cancellationToken);
        var isAcceptedStudioAssistant = studioInvitations.Any(i =>
            i.Status == StudioInvitationStatus.Accepted &&
            i.AssistantUserId == request.AssistantId);

        if (!isAcceptedStudioAssistant)
            throw new UnauthorizedAccessException("Assistant được giao sửa lỗi phải là thành viên đang hoạt động của studio thuộc series này.");

        pin.Status = "InFixing";
        await _bugPinRepo.UpdateAsync(pin, cancellationToken);

        pageTask.ReopenForFix(request.AssistantId, request.Instructions);
        await _pageTaskRepo.UpdateAsync(pageTask, cancellationToken);
        await _pageTaskRepo.SaveChangesAsync(cancellationToken);

        return true;
    }
}
