using MediatR;
using MangaERP.Ranking.Application.Ports;
using MangaERP.Ranking.Domain.Entities;

namespace MangaERP.Ranking.Application.Commands.RefreshRankings;

public record RefreshRankingsCommand() : IRequest<RefreshRankingsResult>;

public record RefreshRankingsResult(bool Success, DateTime RefreshedAt, string Message);

public class RefreshRankingsHandler : IRequestHandler<RefreshRankingsCommand, RefreshRankingsResult>
{
    private readonly IRankingCalculator _calculator;
    private readonly IRankingRepository _rankingRepo;

    public RefreshRankingsHandler(IRankingCalculator calculator, IRankingRepository rankingRepo)
    {
        _calculator = calculator;
        _rankingRepo = rankingRepo;
    }

    public async Task<RefreshRankingsResult> Handle(RefreshRankingsCommand request, CancellationToken cancellationToken)
    {
        var periods = new[] { RankingPeriod.Daily, RankingPeriod.Weekly, RankingPeriod.Monthly, RankingPeriod.AllTime };
        
        foreach (var period in periods)
        {
            var newSnapshots = await _calculator.CalculateAsync(period, 100, cancellationToken);
            await _rankingRepo.ReplaceSnapshotAsync(period, newSnapshots, cancellationToken);
        }

        await _rankingRepo.SaveChangesAsync(cancellationToken);

        return new RefreshRankingsResult(true, DateTime.UtcNow, "Rankings have been successfully refreshed for all periods.");
    }
}
