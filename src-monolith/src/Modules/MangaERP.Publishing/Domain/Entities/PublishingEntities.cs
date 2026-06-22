namespace MangaERP.Publishing.Domain.Entities;

public class PublicationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public Guid SeriesId { get; set; }
    public Guid? PublishedByUserId { get; set; }
    public string IssueType { get; set; } = string.Empty;  // Weekly | Monthly | Special
    public string? PublicationUrl { get; set; }
    public string? CacheKey { get; set; }
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
}

public class Notification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ReceiverId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public bool IsRead { get; set; } = false;
    public string NotifyType { get; set; } = string.Empty;
    public Guid? RelatedEntityId { get; set; }
    public string? RelatedEntityType { get; set; }
    public string? TargetUrl { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
