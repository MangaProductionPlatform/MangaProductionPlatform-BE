using MediatR;
using MangaERP.QA.Application.Ports;

namespace MangaERP.QA.Application.Queries.GetChapterQaSummary;

public record GetChapterQaSummaryQuery(Guid ChapterId) : IRequest<ChapterQaSummaryDto?>;

public record ChapterQaSummaryDto(
    Guid ChapterId,
    int TotalPins,
    int OpenPins,
    int InFixingPins,
    int FixedPins,
    int ResolvedPins,
    bool CanApprove,
    string? SessionStatus
);

public class GetChapterQaSummaryHandler : IRequestHandler<GetChapterQaSummaryQuery, ChapterQaSummaryDto?>
{
    private readonly IBugPinRepository _bugPinRepo;
    private readonly IQASessionRepository _qaSessionRepo;

    public GetChapterQaSummaryHandler(IBugPinRepository bugPinRepo, IQASessionRepository qaSessionRepo)
    {
        _bugPinRepo = bugPinRepo;
        _qaSessionRepo = qaSessionRepo;
    }

    public async Task<ChapterQaSummaryDto?> Handle(GetChapterQaSummaryQuery request, CancellationToken cancellationToken)
    {
        var pins = (await _bugPinRepo.GetByChapterIdAsync(request.ChapterId, cancellationToken)).ToList();
        var session = await _qaSessionRepo.GetByChapterIdAsync(request.ChapterId, cancellationToken);

        var totalPins = pins.Count;
        var openPins = pins.Count(p => p.Status == "Open");
        var inFixingPins = pins.Count(p => p.Status == "InFixing");
        var fixedPins = pins.Count(p => p.Status == "Fixed");
        var resolvedPins = pins.Count(p => p.Status == "Resolved");

        // Can approve only when all pins are resolved (or there are no pins at all)
        var canApprove = totalPins == 0 || pins.All(p => p.Status == "Resolved");

        return new ChapterQaSummaryDto(
            request.ChapterId,
            totalPins,
            openPins,
            inFixingPins,
            fixedPins,
            resolvedPins,
            canApprove,
            session?.Status
        );
    }
}
