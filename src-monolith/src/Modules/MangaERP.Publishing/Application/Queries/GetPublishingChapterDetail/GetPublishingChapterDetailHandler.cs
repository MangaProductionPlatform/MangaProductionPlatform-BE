using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Publishing.Application.Ports;

namespace MangaERP.Publishing.Application.Queries.GetPublishingChapterDetail;

public record GetPublishingChapterDetailQuery(Guid ChapterId) : IRequest<PublishingChapterDetailDto>;

public record PublishingChapterDetailDto(
    Guid ChapterId,
    Guid SeriesId,
    string Title,
    decimal ChapterNumber,
    string Status,
    string? IssueType,
    DateTime? ScheduledPublishAt,
    DateTime? PublishedAt,
    string? PublicationUrl,
    string? CoverImageUrl,
    int TotalPages,
    DateTime CreatedAt
);

public class GetPublishingChapterDetailHandler : IRequestHandler<GetPublishingChapterDetailQuery, PublishingChapterDetailDto>
{
    private readonly IChapterRepository _chapterRepo;
    private readonly IPublicationRecordRepository _pubRepo;

    public GetPublishingChapterDetailHandler(IChapterRepository chapterRepo, IPublicationRecordRepository pubRepo)
    {
        _chapterRepo = chapterRepo;
        _pubRepo = pubRepo;
    }

    public async Task<PublishingChapterDetailDto> Handle(GetPublishingChapterDetailQuery request, CancellationToken cancellationToken)
    {
        var chapter = await _chapterRepo.GetByIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");

        var pubRecord = await _pubRepo.GetByChapterIdAsync(request.ChapterId, cancellationToken);

        return new PublishingChapterDetailDto(
            chapter.Id,
            chapter.SeriesId,
            chapter.Title,
            chapter.ChapterNumber,
            chapter.Status.ToString(),
            chapter.IssueType,
            chapter.ScheduledPublishAt,
            pubRecord?.PublishedAt,
            pubRecord?.PublicationUrl,
            chapter.CoverImageUrl,
            chapter.TotalPages,
            chapter.CreatedAt
        );
    }
}
