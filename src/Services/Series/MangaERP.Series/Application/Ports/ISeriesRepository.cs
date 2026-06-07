using MangaERP.Series.Domain.Entities;

namespace MangaERP.Series.Application.Ports;

public interface ISeriesRepository
{
    Task<MangaSeries?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<MangaSeries>> GetByAuthorIdAsync(Guid authorId, CancellationToken cancellationToken = default);
    Task<IEnumerable<MangaSeries>> GetAllActiveAsync(CancellationToken cancellationToken = default);
    Task AddAsync(MangaSeries series, CancellationToken cancellationToken = default);
    Task UpdateAsync(MangaSeries series, CancellationToken cancellationToken = default);
}
