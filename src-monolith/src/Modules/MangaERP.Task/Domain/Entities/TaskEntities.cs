using MangaERP.Task.Domain.Constants;

namespace MangaERP.Task.Domain.Entities;

public class ArtworkLayer
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PageTaskId { get; set; }
    public Guid AssistantId { get; set; }
    public string LayerType { get; set; } = string.Empty;
    public string FileUrlOriginal { get; set; } = string.Empty;
    public string FileUrlOptimized { get; set; } = string.Empty;
    public int Version { get; set; } = 1;
    public bool IsCurrentVersion { get; set; } = true;
    public string? RejectionNote { get; set; }
    public DateTime? SubmittedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public static ArtworkLayer Submit(
        Guid pageTaskId,
        Guid assistantId,
        string layerType,
        string fileUrlOriginal,
        string fileUrlOptimized,
        int version)
    {
        if (!LayerTypeConstants.IsValid(layerType))
            throw new ArgumentException($"Invalid layer type: {layerType}.");

        return new ArtworkLayer
        {
            PageTaskId = pageTaskId,
            AssistantId = assistantId,
            LayerType = layerType,
            FileUrlOriginal = fileUrlOriginal,
            FileUrlOptimized = string.IsNullOrWhiteSpace(fileUrlOptimized) ? fileUrlOriginal : fileUrlOptimized,
            Version = version,
            IsCurrentVersion = true,
            SubmittedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void MarkRejected(string rejectionNote)
    {
        RejectionNote = rejectionNote;
        ReviewedAt = DateTime.UtcNow;
    }

    public void MarkAccepted()
    {
        RejectionNote = null;
        ReviewedAt = DateTime.UtcNow;
    }

    public string GetDisplayUrl()
        => string.IsNullOrWhiteSpace(FileUrlOptimized) ? FileUrlOriginal : FileUrlOptimized;
}

public class AssistantInvitation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InvitationToken { get; set; } = Guid.NewGuid();
    public string Email { get; set; } = string.Empty;
    public Guid ChapterId { get; set; }
    public Guid InviterMangakaId { get; set; }
    public string AssignedRole { get; set; } = string.Empty;
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

public class DeadlineExtensionRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PageTaskId { get; set; }
    public Guid AssistantId { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime RequestedDeadline { get; set; }
    public string Status { get; set; } = "Pending"; // Pending, Approved, Rejected
    public string? RejectionReason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? HandledAt { get; set; }
}
