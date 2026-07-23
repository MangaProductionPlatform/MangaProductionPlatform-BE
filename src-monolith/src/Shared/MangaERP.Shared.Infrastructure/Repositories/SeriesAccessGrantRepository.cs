using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Shared.Infrastructure.Repositories;

public sealed class SeriesAccessGrantRepository : ISeriesAccessGrantRepository
{
    private readonly AppDbContext _db;

    public SeriesAccessGrantRepository(IDbContextProvider provider)
    {
        _db = (AppDbContext)provider.GetDbContext();
    }

    public async System.Threading.Tasks.Task<SeriesAccessGrant?> GetActiveGrantAsync(Guid collaborationId, Guid seriesId, CancellationToken ct = default)
    {
        return await _db.SeriesAccessGrants
            .FirstOrDefaultAsync(g => g.CollaborationId == collaborationId && g.SeriesId == seriesId && g.RevokedAt == null, ct);
    }

    public async System.Threading.Tasks.Task<SeriesAccessGrant?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        return await _db.SeriesAccessGrants.FirstOrDefaultAsync(g => g.Id == id, ct);
    }

    public async System.Threading.Tasks.Task<IEnumerable<SeriesAccessGrant>> GetByCollaborationIdAsync(Guid collaborationId, CancellationToken ct = default)
    {
        return await _db.SeriesAccessGrants
            .Where(g => g.CollaborationId == collaborationId)
            .OrderByDescending(g => g.GrantedAt)
            .ToListAsync(ct);
    }

    public async System.Threading.Tasks.Task AddAsync(SeriesAccessGrant grant, CancellationToken ct = default)
    {
        await _db.SeriesAccessGrants.AddAsync(grant, ct);
    }

    public System.Threading.Tasks.Task UpdateAsync(SeriesAccessGrant grant, CancellationToken ct = default)
    {
        _db.SeriesAccessGrants.Update(grant);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public async System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        return await _db.SaveChangesAsync(ct);
    }
}
