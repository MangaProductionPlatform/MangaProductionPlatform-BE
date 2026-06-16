using MediatR;
using FluentValidation;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Chapter.Domain.Entities;

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

    public SchedulePublishHandler(IChapterRepository chapterRepo)
    {
        _chapterRepo = chapterRepo;
    }

    public async Task<SchedulePublishResult> Handle(SchedulePublishCommand request, CancellationToken cancellationToken)
    {
        var chapter = await _chapterRepo.GetByIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");

        if (chapter.Status != ChapterStatus.Approved)
            throw new InvalidOperationException("Chỉ có thể lên lịch phát hành cho chương truyện đã được duyệt (Status = Approved).");

        if (request.ScheduledPublishAt <= DateTime.UtcNow)
            throw new ArgumentException("Thời gian lên lịch phát hành phải lớn hơn thời gian hiện tại.");

        chapter.SetPublishSchedule(request.IssueType, request.ScheduledPublishAt);
        await _chapterRepo.UpdateAsync(chapter, cancellationToken);

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
