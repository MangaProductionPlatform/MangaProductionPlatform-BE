using MangaERP.Shared.Domain.Abstractions;

namespace MangaERP.Segmentation.Domain.Entities;

public enum SegmentationTaskType { Shading, Background, LineArt, Effect }
public enum SegmentationTaskStatus { Pending, InProgress, Submitted, Approved, Rejected }

public class SegmentationTask : Entity
{
    public Guid PageId { get; set; }
    public string MaskRle { get; set; } = string.Empty;
    public int[] Bbox { get; set; } = [];
    public SegmentationTaskType TaskType { get; set; }
    public string? Note { get; set; }
    public Guid AssignedToUserId { get; set; }
    public string? AssignedToUserRole { get; set; } // Nhận từ FE để lưu trữ trực tiếp
    public Guid CreatedByUserId { get; set; }
    public SegmentationTaskStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public bool CanTransitionTo(SegmentationTaskStatus newStatus)
    {
        return (Status, newStatus) switch
        {
            (SegmentationTaskStatus.Pending, SegmentationTaskStatus.InProgress) => true,
            (SegmentationTaskStatus.InProgress, SegmentationTaskStatus.Submitted) => true,
            (SegmentationTaskStatus.Submitted, SegmentationTaskStatus.Approved) => true,
            (SegmentationTaskStatus.Submitted, SegmentationTaskStatus.Rejected) => true,
            _ => false
        };
    }

    public void TransitionTo(SegmentationTaskStatus newStatus)
    {
        if (!CanTransitionTo(newStatus))
            throw new InvalidOperationException($"Invalid status transition from {Status} to {newStatus}.");

        Status = newStatus;
        if (newStatus is SegmentationTaskStatus.Approved or SegmentationTaskStatus.Rejected)
        {
            CompletedAt = DateTime.UtcNow;
        }
    }
}
