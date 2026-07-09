using MediatR;
using MangaERP.Chapter.Application.Ports;

namespace MangaERP.Publishing.Application.Queries.GetPublishingSchedule;

public record GetPublishingScheduleQuery() : IRequest<IEnumerable<ScheduledChapterDto>>;

public record ScheduledChapterDto(
    Guid ChapterId,
    Guid SeriesId,
    string Title,
    decimal ChapterNumber,
    string? CoverImageUrl,
    string? IssueType,
    DateTime ScheduledPublishAt,
    DateTime CreatedAt
);

public class GetPublishingScheduleHandler : IRequestHandler<GetPublishingScheduleQuery, IEnumerable<ScheduledChapterDto>>
{
    private readonly IChapterRepository _chapterRepo;

    public GetPublishingScheduleHandler(IChapterRepository chapterRepo)
    {
        _chapterRepo = chapterRepo;
    }

    public async Task<IEnumerable<ScheduledChapterDto>> Handle(GetPublishingScheduleQuery request, CancellationToken cancellationToken)
    {
        // Get only approved chapters that have a scheduled publish date
        var chapters = await _chapterRepo.GetApprovedChaptersAsync(scheduledOnly: true, cancellationToken);

        return chapters.Select(c => new ScheduledChapterDto(
            c.Id,
            c.SeriesId,
            c.Title,
            c.ChapterNumber,
            c.CoverImageUrl,
            c.IssueType,
            c.ScheduledPublishAt!.Value,
            c.CreatedAt
        )).ToList();
    }
}
