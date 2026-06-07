using MediatR;
using MangaERP.Ranking.Domain.Entities;
using MangaERP.BuildingBlocks.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Ranking.Application;

// ─── Repository Port ─────────────────────────────────────────────────────────
public interface IVoteDataRepository
{
    Task AddAsync(VoteData voteData, CancellationToken cancellationToken = default);
    Task<IEnumerable<VoteData>> GetByPeriodAsync(string votePeriod, CancellationToken cancellationToken = default);
}

public interface IRankingSnapshotRepository
{
    Task AddAsync(RankingSnapshot snapshot, CancellationToken cancellationToken = default);
    Task<IEnumerable<RankingSnapshot>> GetByPeriodAsync(string votePeriod, CancellationToken cancellationToken = default);
}

// ─── Command: Import vote data (Editorial Board) ─────────────────────────────
public record ImportVoteDataCommand(
    Guid SeriesId,
    string VotePeriod,
    int VoteCount,
    Guid ImportedBy) : IRequest;

public class ImportVoteDataHandler : IRequestHandler<ImportVoteDataCommand>
{
    private readonly IVoteDataRepository _voteRepo;
    private readonly IRankingSnapshotRepository _snapshotRepo;

    public ImportVoteDataHandler(IVoteDataRepository voteRepo, IRankingSnapshotRepository snapshotRepo)
    {
        _voteRepo = voteRepo;
        _snapshotRepo = snapshotRepo;
    }

    public async Task Handle(ImportVoteDataCommand request, CancellationToken cancellationToken)
    {
        var voteData = new VoteData
        {
            SeriesId = request.SeriesId,
            VotePeriod = request.VotePeriod,
            VoteCount = request.VoteCount,
            ImportedBy = request.ImportedBy,
            ImportedAt = DateTime.UtcNow
        };
        await _voteRepo.AddAsync(voteData, cancellationToken);

        // Re-aggregate rankings for this period
        var allVotes = (await _voteRepo.GetByPeriodAsync(request.VotePeriod, cancellationToken))
            .OrderByDescending(v => v.VoteCount)
            .ToList();

        for (int i = 0; i < allVotes.Count; i++)
        {
            var snapshot = new RankingSnapshot
            {
                SeriesId = allVotes[i].SeriesId,
                VotePeriod = request.VotePeriod,
                Rank = i + 1,
                TotalVotes = allVotes[i].VoteCount,
                CreatedAt = DateTime.UtcNow
            };
            await _snapshotRepo.AddAsync(snapshot, cancellationToken);
        }
    }
}

// ─── Query: Get ranking board for a period ────────────────────────────────────
public record GetRankingBoardQuery(string VotePeriod) : IRequest<IEnumerable<RankingBoardItemDto>>;

public record RankingBoardItemDto(int Rank, Guid SeriesId, int TotalVotes, string VotePeriod);

public class GetRankingBoardHandler : IRequestHandler<GetRankingBoardQuery, IEnumerable<RankingBoardItemDto>>
{
    private readonly IRankingSnapshotRepository _repository;

    public GetRankingBoardHandler(IRankingSnapshotRepository repository) => _repository = repository;

    public async Task<IEnumerable<RankingBoardItemDto>> Handle(GetRankingBoardQuery request, CancellationToken cancellationToken)
    {
        var snapshots = await _repository.GetByPeriodAsync(request.VotePeriod, cancellationToken);
        return snapshots
            .OrderBy(s => s.Rank)
            .Select(s => new RankingBoardItemDto(s.Rank, s.SeriesId, s.TotalVotes, s.VotePeriod));
    }
}
