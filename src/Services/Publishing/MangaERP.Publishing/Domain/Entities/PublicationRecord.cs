namespace MangaERP.Publishing.Domain.Entities;

/// <summary>
/// [NEW] Immutable log of each chapter publish event. Captures CDN URL and Redis cache key for MF8.
/// </summary>
public class PublicationRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid ChapterId { get; set; }
    public Guid SeriesId { get; set; }    // Denormalized for series-level publication history queries
    public Guid? PublishedByUserId { get; set; }  // NULL if auto-published by scheduler
    public string IssueType { get; set; } = string.Empty;  // Weekly | Monthly | Special
    public string PublicationUrl { get; set; } = string.Empty;   // Public CDN URL
    public string CacheKey { get; set; } = string.Empty;         // Redis cache key for MF8 invalidation
    public DateTime PublishedAt { get; set; } = DateTime.UtcNow;
}

