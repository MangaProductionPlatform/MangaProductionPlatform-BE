using MangaERP.Shared.Domain.Abstractions;

namespace MangaERP.Series.Domain.Entities;

public enum SeriesStatus { Active, Hiatus, Cancelled }

public class MangaSeries : AggregateRoot, ISoftDeletable
{
    public string Title { get; private set; } = string.Empty;
    public string? Description { get; private set; }
    public string? CoverImageUrl { get; private set; }
    public string? Genre { get; private set; }
    public SeriesStatus Status { get; private set; } = SeriesStatus.Active;
    public Guid AuthorId { get; private set; }
    public Guid? SubmissionId { get; private set; }
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;

    private MangaSeries() { }

    public static MangaSeries Create(Guid authorId, Guid? submissionId, string title,
        string? description, string? genre, string? coverImageUrl)
        => new() { AuthorId = authorId, SubmissionId = submissionId, Title = title,
                   Description = description, Genre = genre, CoverImageUrl = coverImageUrl,
                   Status = SeriesStatus.Active, CreatedAt = DateTime.UtcNow };

    public void Cancel()
    {
        if (Status == SeriesStatus.Cancelled)
            throw new InvalidOperationException("Series is already cancelled.");
        Status = SeriesStatus.Cancelled;
    }

    public void SetHiatus() => Status = SeriesStatus.Hiatus;
    public void Reactivate() => Status = SeriesStatus.Active;
}
