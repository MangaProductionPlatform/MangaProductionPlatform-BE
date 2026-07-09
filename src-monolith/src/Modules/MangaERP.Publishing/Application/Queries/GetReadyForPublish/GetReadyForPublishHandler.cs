using MediatR;
using MangaERP.Chapter.Application.Ports;

namespace MangaERP.Publishing.Application.Queries.GetReadyForPublish;

public record GetReadyForPublishQuery() : IRequest<IEnumerable<ReadyForPublishChapterDto>>;

public record ReadyForPublishChapterDto(
    Guid ChapterId,
    Guid SeriesId,
    string Title,
    decimal ChapterNumber,
    string? CoverImageUrl,
    string? IssueType,
    DateTime? ScheduledPublishAt,
    DateTime CreatedAt
);

public class GetReadyForPublishHandler : IRequestHandler<GetReadyForPublishQuery, IEnumerable<ReadyForPublishChapterDto>>
{
    private readonly IChapterRepository _chapterRepo;

    public GetReadyForPublishHandler(IChapterRepository chapterRepo)
    {
        _chapterRepo = chapterRepo;
    }

    public async Task<IEnumerable<ReadyForPublishChapterDto>> Handle(GetReadyForPublishQuery request, CancellationToken cancellationToken)
    {
        // Get all approved chapters (both scheduled and unscheduled)
        var chapters = await _chapterRepo.GetApprovedChaptersAsync(scheduledOnly: null, cancellationToken);

        return chapters.Select(c => new ReadyForPublishChapterDto(
            c.Id,
            c.SeriesId,
            c.Title,
            c.ChapterNumber,
            c.CoverImageUrl,
            c.IssueType,
            c.ScheduledPublishAt,
            c.CreatedAt
        )).ToList();
    }
}
