using MangaERP.BuildingBlocks.Domain.Abstractions;
using MangaERP.BuildingBlocks.Infrastructure.Persistence;

namespace MangaERP.Series.Domain.Entities;

public enum SeriesStatus { Active, Hiatus, Cancelled }

/// <summary>
/// MangaSeries aggregate. Created atomically when submission is approved.
/// [FIX-3]: Added Status, CoverImageUrl, Genre.
/// </summary>
public class MangaSeries : AggregateRoot, ISoftDeletable
{
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    // [FIX-3] CRITICAL — Board needs to cancel series
    public string? CoverImageUrl { get; private set; }
    public string? Genre { get; private set; }
    public SeriesStatus Status { get; private set; } = SeriesStatus.Active;

    public Guid AuthorId { get; private set; }
    public Guid? SubmissionId { get; private set; }

    // Soft Delete
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private MangaSeries() { }

    public static MangaSeries Create(Guid authorId, Guid? submissionId, string title, string? description, string? genre, string? coverImageUrl)
    {
        return new MangaSeries
        {
            AuthorId = authorId,
            SubmissionId = submissionId,
            Title = title,
            Description = description,
            Genre = genre,
            CoverImageUrl = coverImageUrl,
            Status = SeriesStatus.Active,
            CreatedAt = DateTime.UtcNow
        };
    }

    public void Cancel()
    {
        if (Status == SeriesStatus.Cancelled)
            throw new InvalidOperationException("Series is already cancelled.");
        Status = SeriesStatus.Cancelled;
        RaiseDomainEvent(new SeriesCancelled(Guid.NewGuid(), DateTime.UtcNow, Id));
    }

    public void SetHiatus() => Status = SeriesStatus.Hiatus;
    public void Reactivate() => Status = SeriesStatus.Active;
}
