namespace MangaERP.Chapter.Domain.Entities;

/// <summary>
/// Flat composite preview page for QA/publishing. [FIX-9]: Added IsPublished.
/// </summary>
public class PreviewPage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PageTaskId { get; set; }
    public string CompositeFileUrl { get; set; } = string.Empty;   // Internal review URL
    public string? ProductionFileUrl { get; set; }                  // CDN public URL

    // [FIX-9] Differentiate internal review from CDN-deployed file
    public bool IsPublished { get; set; } = false;
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual PageTask? PageTask { get; set; }
}
