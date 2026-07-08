using MangaERP.Series.Application.Ports;
using MangaERP.Series.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Shared.Infrastructure.Repositories;

public class SeriesRepository : ISeriesRepository
{
    private readonly AppDbContext _db;

    public SeriesRepository(IDbContextProvider provider)
        => _db = (AppDbContext)provider.GetDbContext();

    public async System.Threading.Tasks.Task<MangaSeries?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.MangaSeries.FirstOrDefaultAsync(s => s.Id == id, ct);

    public async System.Threading.Tasks.Task<IEnumerable<MangaSeries>> GetByAuthorIdAsync(Guid authorId, CancellationToken ct = default)
        => await _db.MangaSeries.Where(s => s.AuthorId == authorId).ToListAsync(ct);

    public async System.Threading.Tasks.Task<IEnumerable<MangaSeries>> GetByManagingTantouIdAsync(Guid tantouEditorId, CancellationToken ct = default)
        => await _db.MangaSeries
            .Where(s => _db.Users.Any(u => u.Id == s.AuthorId && u.ManagingTantouId == tantouEditorId))
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<bool> IsManagedByTantouAsync(Guid seriesId, Guid tantouEditorId, CancellationToken ct = default)
        => await _db.MangaSeries
            .Where(s => s.Id == seriesId)
            .AnyAsync(s => _db.Users.Any(u => u.Id == s.AuthorId && u.ManagingTantouId == tantouEditorId), ct);

    public async System.Threading.Tasks.Task<IEnumerable<MangaSeries>> GetAllAsync(CancellationToken ct = default)
        => await _db.MangaSeries.ToListAsync(ct);

    /// <summary>
    /// Lấy tất cả series đang có yêu cầu hủy ở trạng thái Pending, sắp xếp theo thời gian yêu cầu tăng dần.
    /// </summary>
    public async System.Threading.Tasks.Task<IEnumerable<MangaSeries>> GetCancellationQueueAsync(CancellationToken ct = default)
        => await _db.MangaSeries
            .Where(s => s.CancellationStatus == CancellationRequestStatus.Pending)
            .OrderBy(s => s.CancellationRequestedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<MangaSeries?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default)
        => await _db.MangaSeries.FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);

    public async System.Threading.Tasks.Task AddAsync(MangaSeries series, CancellationToken ct = default)
        => await _db.MangaSeries.AddAsync(series, ct);

    public System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}

