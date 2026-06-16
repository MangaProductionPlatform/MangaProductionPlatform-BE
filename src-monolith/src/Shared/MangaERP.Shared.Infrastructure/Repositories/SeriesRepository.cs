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

    public async System.Threading.Tasks.Task<MangaSeries?> GetBySubmissionIdAsync(Guid submissionId, CancellationToken ct = default)
        => await _db.MangaSeries.FirstOrDefaultAsync(s => s.SubmissionId == submissionId, ct);

    public async System.Threading.Tasks.Task AddAsync(MangaSeries series, CancellationToken ct = default)
        => await _db.MangaSeries.AddAsync(series, ct);

    public System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
