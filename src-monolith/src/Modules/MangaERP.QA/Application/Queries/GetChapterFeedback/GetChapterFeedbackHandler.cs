using MediatR;
using MangaERP.QA.Application.Ports;

namespace MangaERP.QA.Application.Queries.GetChapterFeedback;

public record GetChapterFeedbackQuery(Guid ChapterId) : IRequest<ChapterFeedbackDto>;

public record ChapterFeedbackDto(
    Guid ChapterId,
    IEnumerable<FeedbackBatchDto> Batches
);

public record FeedbackBatchDto(
    Guid BatchToken,
    DateTime SentAt,
    IEnumerable<FeedbackPinDto> Pins
);

public record FeedbackPinDto(
    Guid Id,
    Guid PageTaskId,
    decimal CoordinateX,
    decimal CoordinateY,
    string NoteMessage,
    string? IssueType,
    string Status,
    DateTime CreatedAt
);

public class GetChapterFeedbackHandler : IRequestHandler<GetChapterFeedbackQuery, ChapterFeedbackDto>
{
    private readonly IBugPinRepository _bugPinRepo;

    public GetChapterFeedbackHandler(IBugPinRepository bugPinRepo)
    {
        _bugPinRepo = bugPinRepo;
    }

    public async Task<ChapterFeedbackDto> Handle(GetChapterFeedbackQuery request, CancellationToken cancellationToken)
    {
        var allPins = await _bugPinRepo.GetByChapterIdAsync(request.ChapterId, cancellationToken);

        var batches = allPins
            .GroupBy(p => p.BatchToken)
            .Select(g => new FeedbackBatchDto(
                g.Key,
                g.Max(p => p.CreatedAt),
                g.Select(p => new FeedbackPinDto(
                    p.Id,
                    p.PageTaskId,
                    p.CoordinateX,
                    p.CoordinateY,
                    p.NoteMessage,
                    p.IssueType,
                    p.Status,
                    p.CreatedAt
                )).ToList()
            ))
            .OrderByDescending(b => b.SentAt)
            .ToList();

        return new ChapterFeedbackDto(request.ChapterId, batches);
    }
}
