using MangaERP.Submission.Application.Queries.GetSubmissionStats;
using MangaERP.Series.Application.Queries.GetSeriesStats;
using MediatR;

namespace MangaERP.Api.Queries.GetBoardReports;

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetBoardReportsQuery : IRequest<BoardReportsDto>;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record BoardReportsDto(
    SubmissionReportSection Submissions,
    CancellationReportSection Cancellations,
    DateTime GeneratedAt
);

public record SubmissionReportSection(
    int TotalInReview, int PendingEB, int ConflictEscalated,
    int ApprovedThisMonth, int RejectedThisMonth
);

public record CancellationReportSection(
    int PendingApproval, int ApprovedThisMonth, int RejectedThisMonth
);

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Aggregates cross-module board stats bằng IMediator.Send().
/// KHÔNG inject repository của module khác — tuân thủ guardrail mục 0.3 + 7.5.
/// </summary>
public class GetBoardReportsHandler : IRequestHandler<GetBoardReportsQuery, BoardReportsDto>
{
    private readonly IMediator _mediator;

    public GetBoardReportsHandler(IMediator mediator) => _mediator = mediator;

    public async Task<BoardReportsDto> Handle(
        GetBoardReportsQuery request, CancellationToken ct)
    {
        // Run sequentially because these module queries share the same scoped DbContext.
        var submissionStats = await _mediator.Send(new GetSubmissionStatsQuery(), ct);
        var seriesStats     = await _mediator.Send(new GetSeriesStatsQuery(), ct);

        return new BoardReportsDto(
            Submissions: new SubmissionReportSection(
                TotalInReview:     submissionStats.PendingEBReview + submissionStats.ConflictEscalated,
                PendingEB:         submissionStats.PendingEBReview,
                ConflictEscalated: submissionStats.ConflictEscalated,
                ApprovedThisMonth: submissionStats.ApprovedLast30Days,
                RejectedThisMonth: submissionStats.RejectedLast30Days),
            Cancellations: new CancellationReportSection(
                PendingApproval:     seriesStats.PendingCancellationRequests,
                ApprovedThisMonth:   seriesStats.CancellationApprovedLast30Days,
                RejectedThisMonth:   seriesStats.CancellationRejectedLast30Days),
            GeneratedAt: DateTime.UtcNow
        );
    }
}
