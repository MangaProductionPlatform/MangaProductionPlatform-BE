namespace MangaERP.Task.Domain.Entities;

/// <summary>
/// Assistant invitation entity. [FIX-5]: Added AssignedRole so it can auto-populate ChapterTeams on accept.
/// </summary>
public class AssistantInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvitationToken { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public Guid ChapterId { get; set; }
    public Guid InviterMangakaId { get; set; }

    // [FIX-5] IMPORTANT — role must be set at invite time
    public string AssignedRole { get; set; } = string.Empty;

    public bool IsAccepted { get; set; } = false;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
