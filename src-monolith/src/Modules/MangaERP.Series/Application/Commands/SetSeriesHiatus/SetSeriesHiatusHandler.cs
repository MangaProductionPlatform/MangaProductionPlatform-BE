using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MediatR;

namespace MangaERP.Series.Application.Commands.SetSeriesHiatus;

public record SetSeriesHiatusCommand(Guid SeriesId, Guid AuthorId) : IRequest<SetSeriesHiatusResult>;

public record SetSeriesHiatusResult(Guid SeriesId, string Status);

public class SetSeriesHiatusHandler : IRequestHandler<SetSeriesHiatusCommand, SetSeriesHiatusResult>
{
    private readonly ISeriesRepository _repo;

    public SetSeriesHiatusHandler(ISeriesRepository repo) => _repo = repo;

    public async Task<SetSeriesHiatusResult> Handle(SetSeriesHiatusCommand cmd, CancellationToken ct)
    {
        var series = await _repo.GetByIdAsync(cmd.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {cmd.SeriesId} not found.");

        if (series.AuthorId != cmd.AuthorId)
            throw new UnauthorizedAccessException("You are not allowed to update this series.");

        if (series.Status == SeriesStatus.Cancelled)
            throw new InvalidOperationException("Cannot suspend a cancelled series.");

        series.SetHiatus();

        await _repo.SaveChangesAsync(ct);

        return new SetSeriesHiatusResult(series.Id, series.Status.ToString());
    }
}
