using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MediatR;

namespace MangaERP.Series.Application.Commands.ReactivateSeries;

public record ReactivateSeriesCommand(Guid SeriesId, Guid AuthorId) : IRequest<ReactivateSeriesResult>;

public record ReactivateSeriesResult(Guid SeriesId, string Status);

public class ReactivateSeriesHandler : IRequestHandler<ReactivateSeriesCommand, ReactivateSeriesResult>
{
    private readonly ISeriesRepository _repo;

    public ReactivateSeriesHandler(ISeriesRepository repo) => _repo = repo;

    public async Task<ReactivateSeriesResult> Handle(ReactivateSeriesCommand cmd, CancellationToken ct)
    {
        var series = await _repo.GetByIdAsync(cmd.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {cmd.SeriesId} not found.");

        if (series.AuthorId != cmd.AuthorId)
            throw new UnauthorizedAccessException("You are not allowed to update this series.");

        if (series.Status == SeriesStatus.Cancelled)
            throw new InvalidOperationException("Cannot reactivate a cancelled series.");

        series.Reactivate();

        await _repo.SaveChangesAsync(ct);

        return new ReactivateSeriesResult(series.Id, series.Status.ToString());
    }
}
