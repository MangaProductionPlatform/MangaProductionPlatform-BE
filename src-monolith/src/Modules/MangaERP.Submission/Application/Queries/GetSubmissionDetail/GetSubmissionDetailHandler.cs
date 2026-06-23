using MangaERP.Submission.Application.Ports;
using MangaERP.Identity.Application.Ports;
using MediatR;

namespace MangaERP.Submission.Application.Queries.GetSubmissionDetail;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Lấy chi tiết 1 submission. Dùng bởi Mangaka (chủ sở hữu), TantouEditor, EditorialBoard.
/// </summary>
public record GetSubmissionDetailQuery(
    Guid SubmissionId,
    Guid RequesterId,        // for ownership check when caller is Mangaka
    string RequesterRole     // "Mangaka", "TantouEditor", "EditorialBoard", "Admin"
) : IRequest<SubmissionDetailDto>;

public record SubmitterDto(
    Guid UserId,
    string? FullName,
    string? PenName,
    string? PersonalEmail);

public record SubmissionDetailDto(
    Guid Id,
    string Title,
    string? Description,
    string? Genre,
    string? CoverImageUrl,
    string? ManuscriptUrl,
    Guid SubmitterId,
    SubmitterDto? Submitter,
    string Status,
    string? FeedbackMessage,
    string? EditorRecommendationMessage,
    Guid? AssignedEditorId,
    Guid? ReviewedByUserId,
    DateTime? ReviewedAt,
    DateTime CreatedAt);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetSubmissionDetailHandler
    : IRequestHandler<GetSubmissionDetailQuery, SubmissionDetailDto>
{
    private readonly ISubmissionRepository _repo;
    private readonly IUserRepository _userRepo;

    public GetSubmissionDetailHandler(ISubmissionRepository repo, IUserRepository userRepo)
    {
        _repo = repo;
        _userRepo = userRepo;
    }

    public async Task<SubmissionDetailDto> Handle(
        GetSubmissionDetailQuery query, CancellationToken ct)
    {
        var submission = await _repo.GetByIdAsync(query.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {query.SubmissionId} not found.");

        // Mangaka chỉ xem được submission của mình
        if (query.RequesterRole == "Mangaka"
            && submission.SubmitterId != query.RequesterId)
            throw new UnauthorizedAccessException("You are not the owner of this submission.");

        var submitter = await _userRepo.GetByIdAsync(submission.SubmitterId, ct);
        var submitterDto = submitter is null ? null : new SubmitterDto(
            submitter.Id,
            submitter.FullName,
            submitter.PenName,
            submitter.PersonalEmail);

        return new SubmissionDetailDto(
            submission.Id,
            submission.Title,
            submission.Description,
            submission.Genre,
            submission.CoverImageUrl,
            submission.ManuscriptUrl,
            submission.SubmitterId,
            submitterDto,
            submission.Status.ToString(),
            submission.FeedbackMessage,
            submission.EditorRecommendationMessage,
            submission.AssignedEditorId,
            submission.ReviewedByUserId,
            submission.ReviewedAt,
            submission.CreatedAt);
    }
}
