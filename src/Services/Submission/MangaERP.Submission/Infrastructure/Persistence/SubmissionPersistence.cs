using MangaERP.BuildingBlocks.Infrastructure.Persistence;
using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using MangaERP.Submission.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Submission.Infrastructure.Persistence;

public class SubmissionDbContext : BaseDbContext
{
    public SubmissionDbContext(DbContextOptions<SubmissionDbContext> options) : base(options) { }

    public DbSet<SeriesSubmission> SeriesSubmissions => Set<SeriesSubmission>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Soft delete global filter
        modelBuilder.Entity<SeriesSubmission>().HasQueryFilter(s => !s.IsDeleted);

        modelBuilder.Entity<SeriesSubmission>(entity =>
        {
            entity.ToTable("SeriesSubmissions");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Title).IsRequired().HasMaxLength(256);
            entity.Property(e => e.Genre).HasMaxLength(100);
            entity.Property(e => e.CoverImageUrl).HasMaxLength(2048);
            entity.Property(e => e.ManuscriptUrl).IsRequired().HasMaxLength(2048);
            entity.Property(e => e.Status)
                .HasConversion(v => v.ToString(), v => Enum.Parse<SubmissionStatus>(v))
                .HasMaxLength(50);
            entity.Property(e => e.EditorRecommendationMessage).HasMaxLength(2048);

            // [FIX-2] Board member who reviewed
            entity.HasIndex(e => e.SubmitterId).HasDatabaseName("IX_SeriesSubmissions_SubmitterId");

            // Filtered index for vetting queue (only PENDING, UNDERREVIEW, RECOMMENDEDTOBOARD visible)
            entity.HasIndex(e => e.Status)
                .HasFilter("[Status] IN ('Pending','UnderReview','RecommendedToBoard')")
                .HasDatabaseName("IX_SeriesSubmissions_VettingQueue");
        });
    }
}

public class SubmissionRepository : ISubmissionRepository
{
    private readonly SubmissionDbContext _context;

    public SubmissionRepository(SubmissionDbContext context) => _context = context;

    public async Task<SeriesSubmission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => await _context.SeriesSubmissions.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    public async Task<IEnumerable<SeriesSubmission>> GetBySubmitterAsync(Guid submitterId, CancellationToken cancellationToken = default)
        => await _context.SeriesSubmissions
            .Where(s => s.SubmitterId == submitterId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<SeriesSubmission>> GetVettingQueueAsync(CancellationToken cancellationToken = default)
        => await _context.SeriesSubmissions
            .Where(s => s.Status == SubmissionStatus.Pending || s.Status == SubmissionStatus.UnderReview || s.Status == SubmissionStatus.RecommendedToBoard)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(SeriesSubmission submission, CancellationToken cancellationToken = default)
    {
        await _context.SeriesSubmissions.AddAsync(submission, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(SeriesSubmission submission, CancellationToken cancellationToken = default)
    {
        _context.SeriesSubmissions.Update(submission);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
