using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Series.Application.Commands.ApproveCancellation;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// EB hoặc EIC chấp thuận yêu cầu hủy series.
/// Series sẽ chuyển sang SeriesStatus.Cancelled.
/// </summary>
public record ApproveCancellationCommand(
    Guid SeriesId,
    Guid ReviewerId
) : IRequest<ApproveCancellationResult>;

public record ApproveCancellationResult(
    Guid SeriesId,
    string Title,
    string SeriesStatus,
    string CancellationStatus,
    DateTime ReviewedAt
);

// ── Handler ───────────────────────────────────────────────────────────────────

public class ApproveCancellationHandler
    : IRequestHandler<ApproveCancellationCommand, ApproveCancellationResult>
{
    private readonly ISeriesRepository _repo;

    public ApproveCancellationHandler(ISeriesRepository repo) => _repo = repo;

    public async Task<ApproveCancellationResult> Handle(
        ApproveCancellationCommand command, CancellationToken ct)
    {
        var series = await _repo.GetByIdAsync(command.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {command.SeriesId} không tìm thấy.");

        // Domain logic kiểm tra state
        series.ApproveCancellation(command.ReviewerId);

        await _repo.SaveChangesAsync(ct);

        return new ApproveCancellationResult(
            series.Id,
            series.Title,
            series.Status.ToString(),
            series.CancellationStatus.ToString(),
            series.CancellationReviewedAt!.Value
        );
    }
}
