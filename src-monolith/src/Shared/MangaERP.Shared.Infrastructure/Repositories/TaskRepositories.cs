using MangaERP.Publishing.Application.Ports;
using MangaERP.Publishing.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Shared.Infrastructure.Repositories;

public class ArtworkLayerRepository : IArtworkLayerRepository
{
    private readonly AppDbContext _db;

    public ArtworkLayerRepository(IDbContextProvider provider)
        => _db = (AppDbContext)provider.GetDbContext();

    public async System.Threading.Tasks.Task<ArtworkLayer?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.ArtworkLayers.FirstOrDefaultAsync(l => l.Id == id, ct);

    public async System.Threading.Tasks.Task<ArtworkLayer?> GetCurrentByPageTaskIdAsync(Guid pageTaskId, CancellationToken ct = default)
        => await _db.ArtworkLayers
            .Where(l => l.PageTaskId == pageTaskId && l.IsCurrentVersion)
            .OrderByDescending(l => l.Version)
            .FirstOrDefaultAsync(ct);

    public async System.Threading.Tasks.Task<IEnumerable<ArtworkLayer>> GetByPageTaskIdAsync(Guid pageTaskId, CancellationToken ct = default)
        => await _db.ArtworkLayers
            .Where(l => l.PageTaskId == pageTaskId)
            .OrderByDescending(l => l.Version)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<int> GetMaxVersionAsync(Guid pageTaskId, CancellationToken ct = default)
    {
        var max = await _db.ArtworkLayers
            .Where(l => l.PageTaskId == pageTaskId)
            .MaxAsync(l => (int?)l.Version, ct);
        return max ?? 0;
    }

    public async System.Threading.Tasks.Task AddAsync(ArtworkLayer layer, CancellationToken ct = default)
        => await _db.ArtworkLayers.AddAsync(layer, ct);

    public System.Threading.Tasks.Task UpdateAsync(ArtworkLayer layer, CancellationToken ct = default)
    {
        _db.ArtworkLayers.Update(layer);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public async System.Threading.Tasks.Task MarkPreviousVersionsNotCurrentAsync(Guid pageTaskId, CancellationToken ct = default)
    {
        var layers = await _db.ArtworkLayers
            .Where(l => l.PageTaskId == pageTaskId && l.IsCurrentVersion)
            .ToListAsync(ct);

        foreach (var layer in layers)
            layer.IsCurrentVersion = false;
    }

    public System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}

