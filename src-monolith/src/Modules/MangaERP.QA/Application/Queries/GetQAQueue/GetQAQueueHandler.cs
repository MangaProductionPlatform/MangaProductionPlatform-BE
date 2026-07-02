using MediatR;
using MangaERP.Chapter.Application.Ports;

namespace MangaERP.QA.Application.Queries.GetQAQueue;

public record GetQAQueueQuery(Guid EditorId) : IRequest<IEnumerable<QAQueueChapterDto>>;

public record QAQueueChapterDto(
    Guid ChapterId,
    Guid SeriesId,
    string Title,
    decimal ChapterNumber,
    string? CoverImageUrl,
    DateTime SubmittedAt
);

public class GetQAQueueHandler : IRequestHandler<GetQAQueueQuery, IEnumerable<QAQueueChapterDto>>
{
    private readonly IChapterRepository _chapterRepo;

    public GetQAQueueHandler(IChapterRepository chapterRepo)
    {
        _chapterRepo = chapterRepo;
    }

    public async Task<IEnumerable<QAQueueChapterDto>> Handle(GetQAQueueQuery request, CancellationToken cancellationToken)
    {
        var chapters = await _chapterRepo.GetQAQueueAsync(request.EditorId, cancellationToken);
        
        return chapters.Select(c => new QAQueueChapterDto(
            c.Id,
            c.SeriesId,
            c.Title,
            c.ChapterNumber,
            c.CoverImageUrl,
            c.CreatedAt
        )).ToList();
    }
}
