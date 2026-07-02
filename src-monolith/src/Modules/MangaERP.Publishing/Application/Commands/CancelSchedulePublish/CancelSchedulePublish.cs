using MediatR;
using MangaERP.Chapter.Application.Ports;

namespace MangaERP.Publishing.Application.Commands.CancelSchedulePublish;

public record CancelSchedulePublishCommand(Guid ChapterId) : IRequest<bool>;

public class CancelSchedulePublishHandler : IRequestHandler<CancelSchedulePublishCommand, bool>
{
    private readonly IChapterRepository _chapterRepo;

    public CancelSchedulePublishHandler(IChapterRepository chapterRepo)
    {
        _chapterRepo = chapterRepo;
    }

    public async Task<bool> Handle(CancelSchedulePublishCommand request, CancellationToken cancellationToken)
    {
        var chapter = await _chapterRepo.GetByIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");

        if (chapter.Status != MangaERP.Chapter.Domain.Entities.ChapterStatus.Approved)
            throw new InvalidOperationException("Chỉ có thể hủy lịch phát hành cho chương truyện đã được duyệt (Status = Approved).");

        chapter.CancelPublishSchedule();
        await _chapterRepo.UpdateAsync(chapter, cancellationToken);
        await _chapterRepo.SaveChangesAsync(cancellationToken);

        return true;
    }
}
