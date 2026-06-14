using MangaERP.Task.Domain.Entities;

namespace MangaERP.Task.Application.Ports;

public interface IArtworkLayerRepository
{
    System.Threading.Tasks.Task<ArtworkLayer?> GetByIdAsync(Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<ArtworkLayer?> GetCurrentByPageTaskIdAsync(Guid pageTaskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<ArtworkLayer>> GetByPageTaskIdAsync(Guid pageTaskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> GetMaxVersionAsync(Guid pageTaskId, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(ArtworkLayer layer, CancellationToken ct = default);
    System.Threading.Tasks.Task UpdateAsync(ArtworkLayer layer, CancellationToken ct = default);
    System.Threading.Tasks.Task MarkPreviousVersionsNotCurrentAsync(Guid pageTaskId, CancellationToken ct = default);
    System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default);
}
