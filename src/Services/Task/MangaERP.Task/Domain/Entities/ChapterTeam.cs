namespace MangaERP.Task.Domain.Entities;

/// <summary>
/// Chapter team membership. [FIX-6]: Added CreatedAt, InvitedByUserId for audit trail.
/// </summary>
public class ChapterTeam
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public Guid UserId { get; set; }
    public string AssignedRole { get; set; } = string.Empty;  // LineArt | Background | Coloring | VFX

    // [FIX-6] Audit trail for team membership
    public Guid? InvitedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
