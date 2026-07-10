using MangaERP.Identity.Application.Queries.GetUserStats;
using MangaERP.Submission.Application.Queries.GetSubmissionStats;
using MangaERP.Series.Application.Queries.GetSeriesStats;
using MediatR;

namespace MangaERP.Api.Queries.GetAdminDashboard;

// ── Query ─────────────────────────────────────────────────────────────────────

public record GetAdminDashboardQuery : IRequest<AdminDashboardDto>;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record AdminDashboardDto(
    UserStatsDto       UserStats,
    SubmissionStatsDto SubmissionStats,
    SeriesStatsDto     SeriesStats,
    DateTime           GeneratedAt
);

public record UserStatsDto(
    int TotalUsers, int ActiveUsers, int PendingActivation, int SuspendedUsers,
    int TotalMangaka, int TotalAssistants, int TotalTantouEditors,
    int TotalEditorialBoard, int TotalEditorInChief, int TotalAdmins
);

public record SubmissionStatsDto(
    int TotalSubmissions, int Draft, int PendingEBReview,
    int RequiresRevision, int ConflictEscalated, int EBApproved, int EBRejected
);

public record SeriesStatsDto(
    int TotalSeries, int Active, int Hiatus, int Cancelled,
    int PendingCancellationRequests
);

// ── Handler ───────────────────────────────────────────────────────────────────

/// <summary>
/// Aggregates cross-module stats bằng cách gọi IMediator.Send() cho từng module.
/// KHÔNG inject repository của module khác — tuân thủ guardrail mục 0.3 + 7.5.
/// Mỗi module-internal Query tự biết cách truy vấn dữ liệu của nó.
/// </summary>
public class GetAdminDashboardHandler : IRequestHandler<GetAdminDashboardQuery, AdminDashboardDto>
{
    private readonly IMediator _mediator;

    public GetAdminDashboardHandler(IMediator mediator) => _mediator = mediator;

    public async Task<AdminDashboardDto> Handle(
        GetAdminDashboardQuery request, CancellationToken ct)
    {
        // Run sequentially because these module queries share the same scoped DbContext.
        var userResult       = await _mediator.Send(new GetUserStatsQuery(), ct);
        var submissionResult = await _mediator.Send(new GetSubmissionStatsQuery(), ct);
        var seriesResult     = await _mediator.Send(new GetSeriesStatsQuery(), ct);

        return new AdminDashboardDto(
            new UserStatsDto(
                userResult.TotalUsers, userResult.ActiveUsers,
                userResult.PendingActivation, userResult.SuspendedUsers,
                userResult.TotalMangaka, userResult.TotalAssistants,
                userResult.TotalTantouEditors, userResult.TotalEditorialBoard,
                userResult.TotalEditorInChief, userResult.TotalAdmins),
            new SubmissionStatsDto(
                submissionResult.TotalSubmissions, submissionResult.Draft,
                submissionResult.PendingEBReview, submissionResult.RequiresRevision,
                submissionResult.ConflictEscalated, submissionResult.EBApproved,
                submissionResult.EBRejected),
            new SeriesStatsDto(
                seriesResult.TotalSeries, seriesResult.Active,
                seriesResult.Hiatus, seriesResult.Cancelled,
                seriesResult.PendingCancellationRequests),
            GeneratedAt: DateTime.UtcNow
        );
    }
}
