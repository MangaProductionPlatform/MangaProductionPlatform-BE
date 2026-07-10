using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Series.Application.Ports;
using MangaERP.QA.Application.Ports;

namespace MangaERP.QA.Application.Commands.ReportBugPinFixed;

public record ReportBugPinFixedCommand(Guid PinId, Guid UserId) : IRequest<bool>;

public class ReportBugPinFixedHandler : IRequestHandler<ReportBugPinFixedCommand, bool>
{
    private readonly IBugPinRepository _bugPinRepo;
    private readonly IPageTaskRepository _pageTaskRepo;
    private readonly IChapterRepository _chapterRepo;
    private readonly ISeriesRepository _seriesRepo;

    public ReportBugPinFixedHandler(
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

    public async Task<bool> Handle(ReportBugPinFixedCommand request, CancellationToken cancellationToken)
    {
        var pin = await _bugPinRepo.GetByIdAsync(request.PinId, cancellationToken)
            ?? throw new KeyNotFoundException($"BugPin {request.PinId} not found.");

        var pageTask = await _pageTaskRepo.GetByIdAsync(pin.PageTaskId, cancellationToken)
            ?? throw new KeyNotFoundException($"PageTask {pin.PageTaskId} not found.");

        var chapter = await _chapterRepo.GetByIdAsync(pin.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {pin.ChapterId} not found.");

        var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, cancellationToken)
            ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

        if (pageTask.AssignedAssistantId != request.UserId && series.AuthorId != request.UserId)
            throw new UnauthorizedAccessException("Bạn không có quyền báo cáo sửa lỗi (không phải Assistant được giao việc hoặc Mangaka).");

        if (pin.Status != "InFixing")
            throw new InvalidOperationException("Chỉ có thể báo cáo đã sửa lỗi với pin đang ở trạng thái InFixing.");

        pin.Status = "Fixed";
        await _bugPinRepo.UpdateAsync(pin, cancellationToken);

        return true;
    }
}
