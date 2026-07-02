using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using MediatR;

namespace MangaERP.Submission.Application.Queries.GetSubmissionVotes;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// EB/EIC/Admin xem kết quả phiếu bầu của một submission theo vòng.
/// Route: GET /api/v1/submissions/{id}/votes?round=1
/// Nếu không có round → trả về vòng hiện tại (CurrentRound).
/// </summary>
public record GetSubmissionVotesQuery(
    Guid   SubmissionId,
    Guid   RequesterId,
    string RequesterRole,
    int?   Round = null
) : IRequest<SubmissionVotesDto>;

// ── DTOs ──────────────────────────────────────────────────────────────────────

public record SubmissionVotesDto(
    Guid   SubmissionId,
    string SubmissionTitle,
    int    Round,
    int    TotalVotes,
    int    ApproveCount,
    int    RejectCount,
    int    RevisionCount,
    IEnumerable<VoteDetailDto> Votes
);

public record VoteDetailDto(
    Guid     EditorId,
    string   VoteType,
    string?  Comment,
    DateTime VotedAt
);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetSubmissionVotesHandler
    : IRequestHandler<GetSubmissionVotesQuery, SubmissionVotesDto>
{
    private readonly ISubmissionRepository _repo;

    public GetSubmissionVotesHandler(ISubmissionRepository repo) => _repo = repo;

    public async Task<SubmissionVotesDto> Handle(
        GetSubmissionVotesQuery query, CancellationToken ct)
    {
        // Chỉ EB, EIC mới được xem phiếu bầu
        var allowedRoles = new[] { "EditorialBoard", "EditorInChief" };
        if (!allowedRoles.Contains(query.RequesterRole))
            throw new UnauthorizedAccessException("Chỉ Editorial Board hoặc EIC mới được xem phiếu bầu.");

        var submission = await _repo.GetByIdAsync(query.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {query.SubmissionId} không tìm thấy.");

        var round = query.Round ?? submission.CurrentRound;

        var votes = (await _repo.GetVotesByRoundAsync(query.SubmissionId, round, ct)).ToList();

        var voteDtos = votes.Select(v => new VoteDetailDto(
            v.EditorId,
            v.VoteType.ToString(),
            v.Comment,
            v.VotedAt
        ));

        return new SubmissionVotesDto(
            submission.Id,
            submission.Title,
            round,
            TotalVotes:    votes.Count,
            ApproveCount:  votes.Count(v => v.VoteType == VoteType.APPROVE),
            RejectCount:   votes.Count(v => v.VoteType == VoteType.REJECT),
            RevisionCount: votes.Count(v => v.VoteType == VoteType.REQ_REVISION),
            Votes:         voteDtos
        );
    }
}
