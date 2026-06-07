using MangaERP.Task.Domain.Enums;

namespace MangaERP.Task.Domain.Entities;

/// <summary>
/// Artwork layer asset. [FIX-8]: Added RejectionNote, SubmittedAt, ReviewedAt.
/// Versioned for MF7 audit trail (IsCurrentVersion flag).
/// </summary>
public class ArtworkLayer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PageTaskId { get; set; }
    public Guid AssistantId { get; set; }
    public LayerType LayerType { get; set; }
    public string FileUrlOriginal { get; set; } = string.Empty;
    public string FileUrlOptimized { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool IsCurrentVersion { get; set; } = true;

    // [FIX-8] Rejection feedback and timestamps
    public string? RejectionNote { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
