namespace MangaERP.Submission.Domain.Entities;

public enum VoteType
{
    APPROVE,
    REJECT,
    REQ_REVISION
}

/// <summary>
/// Lưu vết lịch sử bỏ phiếu của từng thành viên Editorial Board cho một submission.
/// Mỗi editor chỉ được vote 1 lần mỗi RoundNumber.
/// </summary>
public class SubmissionVote
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public Guid SubmissionId { get; private set; }
    public Guid EditorId { get; private set; }
    public VoteType VoteType { get; private set; }
    public string? Comment { get; private set; }
    public DateTime VotedAt { get; private set; } = DateTime.UtcNow;

    /// <summary>
    /// Vòng bỏ phiếu — bắt đầu từ 1, tăng lên mỗi khi EIC yêu cầu chỉnh sửa lại.
    /// Dùng để kiểm tra editor đã vote cho vòng hiện tại chưa.
    /// </summary>
    public int RoundNumber { get; private set; } = 1;

    private SubmissionVote() { }

    public static SubmissionVote Create(
        Guid submissionId,
        Guid editorId,
        VoteType voteType,
        string? comment,
        int roundNumber)
    {
        if (roundNumber < 1)
            throw new ArgumentException("RoundNumber phải >= 1.");

        return new SubmissionVote
        {
            SubmissionId = submissionId,
            EditorId = editorId,
            VoteType = voteType,
            Comment = comment,
            RoundNumber = roundNumber,
            VotedAt = DateTime.UtcNow
        };
    }
}
