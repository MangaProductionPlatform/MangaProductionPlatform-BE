using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Series.Application.Commands.RejectCancellation;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// EB hoặc EIC từ chối yêu cầu hủy series.
/// Series giữ nguyên trạng thái Active/Hiatus.
/// </summary>
public record RejectCancellationCommand(
    Guid SeriesId,
    Guid ReviewerId,
    string RejectReason
) : IRequest<RejectCancellationResult>;

public record RejectCancellationResult(
    Guid SeriesId,
    string Title,
    string SeriesStatus,
    string CancellationStatus,
    string RejectReason,
    DateTime ReviewedAt
);

// ── Handler ───────────────────────────────────────────────────────────────────

public class RejectCancellationHandler
    : IRequestHandler<RejectCancellationCommand, RejectCancellationResult>
{
    private readonly ISeriesRepository _repo;

    public RejectCancellationHandler(ISeriesRepository repo) => _repo = repo;

    public async Task<RejectCancellationResult> Handle(
        RejectCancellationCommand command, CancellationToken ct)
    {
        var series = await _repo.GetByIdAsync(command.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {command.SeriesId} không tìm thấy.");

        // Domain logic kiểm tra state và validate reason
        series.RejectCancellation(command.ReviewerId, command.RejectReason);

        await _repo.SaveChangesAsync(ct);

        return new RejectCancellationResult(
            series.Id,
            series.Title,
            series.Status.ToString(),
            series.CancellationStatus.ToString(),
            series.CancellationRejectReason!,
            series.CancellationReviewedAt!.Value
        );
    }
}
