using MangaERP.Shared.Domain.Abstractions;

namespace MangaERP.Task.Domain.Entities;

public class ArtworkLayer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PageTaskId { get; set; }
    public Guid AssistantId { get; set; }
    public string LayerType { get; set; } = string.Empty;  // LineArt | Background | Coloring | Text
    public string FileUrlOriginal { get; set; } = string.Empty;
    public string FileUrlOptimized { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool IsCurrentVersion { get; set; } = true;
    public string? RejectionNote { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class AssistantInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvitationToken { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public Guid ChapterId { get; set; }
    public Guid InviterMangakaId { get; set; }
    public string AssignedRole { get; set; } = string.Empty;  // LineArt | Background | Coloring | VFX
    public bool IsAccepted { get; set; } = false;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class ChapterTeam
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public Guid UserId { get; set; }
    public string AssignedRole { get; set; } = string.Empty;
    public Guid? InvitedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
