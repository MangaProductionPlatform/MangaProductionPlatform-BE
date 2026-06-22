using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Submission.Application.Ports;
using MangaERP.Submission.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Shared.Infrastructure.Repositories;

public class SubmissionRepository : ISubmissionRepository
{
    private readonly AppDbContext _db;

    public SubmissionRepository(IDbContextProvider provider)
        => _db = (AppDbContext)provider.GetDbContext();

    public async System.Threading.Tasks.Task<SeriesSubmission?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.SeriesSubmissions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async System.Threading.Tasks.Task<IEnumerable<SeriesSubmission>> GetBySubmitterIdAsync(Guid submitterId, CancellationToken ct = default)
        => await _db.SeriesSubmissions
            .Where(s => s.SubmitterId == submitterId)
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<IEnumerable<SeriesSubmission>> GetRecommendedQueueAsync(CancellationToken ct = default)
        => await _db.SeriesSubmissions
            .Where(s => s.Status == SubmissionStatus.Pending_EB_Review)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<bool> HasActiveSubmissionAsync(Guid submitterId, string title, CancellationToken ct = default)
        => await _db.SeriesSubmissions.AnyAsync(s => 
            s.SubmitterId == submitterId && 
            s.Title.ToLower() == title.ToLower() && 
            s.Status != SubmissionStatus.EB_Approved && 
            s.Status != SubmissionStatus.EB_Rejected, 
            ct);

    public async System.Threading.Tasks.Task AddAsync(SeriesSubmission submission, CancellationToken ct = default)
        => await _db.SeriesSubmissions.AddAsync(submission, ct);

    public System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);

    // ── Feedback Pins ─────────────────────────────────────────────────────────

    public async System.Threading.Tasks.Task<IEnumerable<SubmissionFeedbackPin>> GetActivePinsBySubmissionIdAsync(
        Guid submissionId, CancellationToken ct = default)
        => await _db.SubmissionFeedbackPins
            .Where(p => p.SubmissionId == submissionId && !p.IsArchived)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<IEnumerable<SubmissionFeedbackPin>> GetAllPinsBySubmissionIdAsync(
        Guid submissionId, CancellationToken ct = default)
        => await _db.SubmissionFeedbackPins
            .Where(p => p.SubmissionId == submissionId)
            .OrderBy(p => p.CreatedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task AddPinAsync(SubmissionFeedbackPin pin, CancellationToken ct = default)
        => await _db.SubmissionFeedbackPins.AddAsync(pin, ct);
}
