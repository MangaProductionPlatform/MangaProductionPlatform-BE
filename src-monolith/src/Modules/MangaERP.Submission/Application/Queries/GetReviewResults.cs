using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using MediatR;

namespace MangaERP.Submission.Application.Queries.GetReviewResults;

public record GetReviewResultsQuery(
    Guid SubmissionId,
    Guid RequesterId,
    int? Round = null
) : IRequest<ReviewResultsDto>;

public record ReviewResultsDto(
    Guid SubmissionId,
    string SubmissionTitle,
    int Round,
    string Status,
    string? EiCFeedback,
    int ApproveCount,
    int RejectCount,
    int RevisionCount,
    IEnumerable<ReviewCommentDto> Comments
);

public record ReviewCommentDto(
    string VoteType,
    string? Comment,
    DateTime VotedAt
);

public class GetReviewResultsHandler : IRequestHandler<GetReviewResultsQuery, ReviewResultsDto>
{
    private readonly ISubmissionRepository _repo;

    public GetReviewResultsHandler(ISubmissionRepository repo)
    {
        _repo = repo;
    }

    public async Task<ReviewResultsDto> Handle(GetReviewResultsQuery query, CancellationToken ct)
    {
        var submission = await _repo.GetByIdAsync(query.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {query.SubmissionId} không tìm thấy.");

        // Mangaka can only view their own submissions
        if (submission.SubmitterId != query.RequesterId)
        {
            throw new UnauthorizedAccessException("Bạn không có quyền xem kết quả duyệt của submission này.");
        }

        var round = query.Round ?? submission.CurrentRound;
        var votes = (await _repo.GetVotesByRoundAsync(query.SubmissionId, round, ct)).ToList();

        // Anonymize editors for mangaka
        var comments = votes.Select(v => new ReviewCommentDto(
            v.VoteType.ToString(),
            v.Comment,
            v.VotedAt
        ));

        // Get conflict resolution feedback if any for this round.
        // Assuming we just use submission.Reason or add a specific query for conflict resolution.
        // For simplicity, we just use the submission status.

        return new ReviewResultsDto(
            submission.Id,
            submission.Title,
            round,
            submission.Status.ToString(),
            null, // EiCFeedback could be fetched if there's a specific table/field for it
            votes.Count(v => v.VoteType == VoteType.APPROVE),
            votes.Count(v => v.VoteType == VoteType.REJECT),
            votes.Count(v => v.VoteType == VoteType.REQ_REVISION),
            comments
        );
    }
}
