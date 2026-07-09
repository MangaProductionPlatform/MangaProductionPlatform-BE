using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using MediatR;

namespace MangaERP.Submission.Application.Commands.DeleteDraft;

// ── Command ───────────────────────────────────────────────────────────────────

/// <summary>
/// Mangaka xóa mềm (soft delete) một Draft submission của mình.
/// Chỉ được phép khi status == Draft.
/// </summary>
public record DeleteDraftCommand(
    Guid SubmissionId,
    Guid RequesterId
) : IRequest<DeleteDraftResult>;

public record DeleteDraftResult(
    Guid SubmissionId,
    string Message
);

// ── Handler ───────────────────────────────────────────────────────────────────

public class DeleteDraftHandler : IRequestHandler<DeleteDraftCommand, DeleteDraftResult>
{
    private readonly ISubmissionRepository _repo;

    public DeleteDraftHandler(ISubmissionRepository repo) => _repo = repo;

    public async Task<DeleteDraftResult> Handle(
        DeleteDraftCommand command, CancellationToken ct)
    {
        var submission = await _repo.GetByIdAsync(command.SubmissionId, ct)
            ?? throw new KeyNotFoundException($"Submission {command.SubmissionId} không tìm thấy.");

        if (submission.SubmitterId != command.RequesterId)
            throw new UnauthorizedAccessException("Bạn chỉ được xóa submission của chính mình.");

        if (submission.Status != SubmissionStatus.Draft)
            throw new InvalidOperationException(
                $"Chỉ có thể xóa submission ở trạng thái Draft. Trạng thái hiện tại: {submission.Status}.");

        // Soft delete — IsDeleted = true, DeletedAt = UtcNow (handled by AppDbContext.SaveChangesAsync)
        submission.IsDeleted = true;
        submission.DeletedAt = DateTime.UtcNow;

        await _repo.SaveChangesAsync(ct);

        return new DeleteDraftResult(
            command.SubmissionId,
            "Draft submission đã được xóa thành công."
        );
    }
}
