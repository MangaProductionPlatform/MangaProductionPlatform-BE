using MangaERP.Submission.Application.Ports;
using MangaERP.Identity.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using MediatR;

namespace MangaERP.Submission.Application.Queries.GetSubmissionDetail;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Lấy chi tiết 1 submission. Dùng bởi Mangaka (chủ sở hữu), EditorialBoard, EditorInChief, Admin.
/// TantouEditor không tham gia SeriesSubmission (Mainflow 1) — dùng Chapter QA endpoints cho Mainflow 3.
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
        var isMangaka = query.RequesterRole == "Mangaka";
        // Mangaka thấy:
        //   - FeedbackMessage khi EB đã ra quyết định (Approved/Rejected/Conflict)
        //   - null khi đang trong quá trình review (double-blind)
        // Staff (EB/EIC/Admin) thấy toàn bộ FeedbackMessage.
        var visibleFeedback = isMangaka
            ? submission.Status is SubmissionStatus.EB_Rejected
                                or SubmissionStatus.EB_Approved
                                or SubmissionStatus.Conflict_Escalated
                ? submission.FeedbackMessage
                : null
            : submission.FeedbackMessage;

        return new SubmissionDetailDto(
            submission.Id,
            submission.Title,
            submission.Description,
            submission.Genre,
            MangaERP.Shared.Application.Helpers.MediaUrlSanitizer.Sanitize(submission.CoverImageUrl),
            MangaERP.Shared.Application.Helpers.MediaUrlSanitizer.Sanitize(submission.ManuscriptUrl),
            submission.SubmitterId,
            submitterDto,
            submission.Status.ToString(),
            visibleFeedback,
            submission.EditorRecommendationMessage,
            submission.AssignedEditorId,
            isMangaka ? null : submission.ReviewedByUserId,
            isMangaka ? null : submission.ReviewedAt,
            submission.CreatedAt);
    }
}
