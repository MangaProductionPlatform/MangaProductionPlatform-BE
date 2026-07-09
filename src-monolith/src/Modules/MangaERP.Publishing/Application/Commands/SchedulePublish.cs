using MediatR;
using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;
using MangaERP.Publishing.Application.Services;

namespace MangaERP.Publishing.Application.Commands;

public record SchedulePublishCommand(
    Guid ChapterId,
    Guid SeriesId,
    string IssueType,
    DateTime ScheduledPublishAt
) : IRequest<SchedulePublishResult>;

public record SchedulePublishResult(Guid ChapterId, string Status, string IssueType, DateTime ScheduledPublishAt);

public class SchedulePublishHandler : IRequestHandler<SchedulePublishCommand, SchedulePublishResult>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPublishingConflictChecker _conflictChecker;

    public SchedulePublishHandler(IChapterRepository chapterRepo, IPublishingConflictChecker conflictChecker)
    {
        _chapterRepo = chapterRepo;
        _conflictChecker = conflictChecker;
    }

    public async Task<SchedulePublishResult> Handle(SchedulePublishCommand request, CancellationToken cancellationToken)
    {
        var chapter = await _chapterRepo.GetByIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");

        if (chapter.SeriesId != request.SeriesId)
            throw new InvalidOperationException("SeriesId không khớp với chapter. Vui lòng kiểm tra lại.");

        if (chapter.Status != ChapterStatus.Approved)
            throw new InvalidOperationException("Chỉ có thể lên lịch phát hành cho chương truyện đã được duyệt (Status = Approved).");

        var conflict = await _conflictChecker.CheckAsync(request.SeriesId, request.ScheduledPublishAt, request.ChapterId, cancellationToken);
        if (conflict.HasConflict)
            throw new InvalidOperationException(conflict.ConflictMessage);

        if (request.ScheduledPublishAt <= DateTime.UtcNow)
            throw new ArgumentException("Thời gian lên lịch phát hành phải lớn hơn thời gian hiện tại.");

        chapter.SetPublishSchedule(request.IssueType, request.ScheduledPublishAt);
        await _chapterRepo.UpdateAsync(chapter, cancellationToken);
        await _chapterRepo.SaveChangesAsync(cancellationToken);

        return new SchedulePublishResult(
            chapter.Id,
            chapter.Status.ToString(),
            chapter.IssueType ?? request.IssueType,
            chapter.ScheduledPublishAt.Value
        );
    }
}

public class SchedulePublishValidator : AbstractValidator<SchedulePublishCommand>
{
    public SchedulePublishValidator()
    {
        RuleFor(x => x.ChapterId).NotEmpty();
        RuleFor(x => x.SeriesId).NotEmpty();
        RuleFor(x => x.IssueType).Must(x => x == "Weekly" || x == "Monthly" || x == "Special")
            .WithMessage("IssueType phải là Weekly, Monthly hoặc Special.");
        RuleFor(x => x.ScheduledPublishAt).NotEmpty();
    }
}
