using MangaERP.Series.Application.Ports;
using MediatR;

namespace MangaERP.Series.Application.Commands.RequestCancellation;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Mangaka gửi yêu cầu hủy series của mình.
/// SeriesId và RequesterId (từ JWT) được truyền vào command.
/// </summary>
public record RequestCancellationCommand(
    Guid SeriesId,
    Guid RequesterId,
    string Reason
) : IRequest<RequestCancellationResult>;

public record RequestCancellationResult(
    Guid SeriesId,
    string Title,
    string CancellationStatus,
    DateTime RequestedAt
);

// ── Handler ───────────────────────────────────────────────────────────────────

public class RequestCancellationHandler
    : IRequestHandler<RequestCancellationCommand, RequestCancellationResult>
{
    private readonly ISeriesRepository _repo;

    public RequestCancellationHandler(ISeriesRepository repo) => _repo = repo;

    public async Task<RequestCancellationResult> Handle(
        RequestCancellationCommand command, CancellationToken ct)
    {
        var series = await _repo.GetByIdAsync(command.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {command.SeriesId} không tìm thấy.");

        // Domain logic kiểm tra ownership + state
        series.RequestCancellation(command.RequesterId, command.Reason);

        await _repo.SaveChangesAsync(ct);

        return new RequestCancellationResult(
            series.Id,
            series.Title,
            series.CancellationStatus.ToString(),
            series.CancellationRequestedAt!.Value
        );
    }
}
