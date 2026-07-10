using MediatR;
using MangaERP.Chapter.Application.Ports;
using MangaERP.Publishing.Application.Ports;
using MangaERP.Series.Application.Ports;

namespace MangaERP.Publishing.Application.Queries.GetPublishingChapterDetail;

public record GetPublishingChapterDetailQuery(
    Guid ChapterId,
    Guid RequesterId,
    bool CanViewAllPublishingData) : IRequest<PublishingChapterDetailDto>;

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
    private readonly ISeriesRepository _seriesRepo;

    public GetPublishingChapterDetailHandler(
        IChapterRepository chapterRepo,
        IPublicationRecordRepository pubRepo,
        ISeriesRepository seriesRepo)
    {
        _chapterRepo = chapterRepo;
        _pubRepo = pubRepo;
        _seriesRepo = seriesRepo;
    }

    public async Task<PublishingChapterDetailDto> Handle(GetPublishingChapterDetailQuery request, CancellationToken cancellationToken)
    {
        var chapter = await _chapterRepo.GetByIdAsync(request.ChapterId, cancellationToken)
            ?? throw new KeyNotFoundException($"Chapter {request.ChapterId} not found.");

        if (!request.CanViewAllPublishingData)
        {
            var series = await _seriesRepo.GetByIdAsync(chapter.SeriesId, cancellationToken)
                ?? throw new KeyNotFoundException($"Series {chapter.SeriesId} not found.");

            var isAuthor = series.AuthorId == request.RequesterId;
            var isAssignedEditor = chapter.AssignedEditorId == request.RequesterId;

            if (!isAuthor && !isAssignedEditor)
                throw new UnauthorizedAccessException("Bạn không có quyền xem chi tiết phát hành của chapter này.");
        }

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
