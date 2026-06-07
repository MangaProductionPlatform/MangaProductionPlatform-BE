using MangaERP.QA.Domain.Entities;

namespace MangaERP.QA.Application.Ports;

public interface IBugPinRepository
{
    Task<BugPin?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<BugPin>> GetByChapterIdAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task<IEnumerable<BugPin>> GetByBatchTokenAsync(Guid batchToken, CancellationToken cancellationToken = default);
    Task<bool> HasUnresolvedPinsAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task AddAsync(BugPin bugPin, CancellationToken cancellationToken = default);
    Task UpdateAsync(BugPin bugPin, CancellationToken cancellationToken = default);
    Task DeleteAsync(BugPin bugPin, CancellationToken cancellationToken = default);
    Task ResolveAllForChapterAsync(Guid chapterId, CancellationToken cancellationToken = default);
}

public interface IQASessionRepository
{
    Task<QASession?> GetByChapterIdAsync(Guid chapterId, CancellationToken cancellationToken = default);
    Task AddAsync(QASession session, CancellationToken cancellationToken = default);
    Task UpdateAsync(QASession session, CancellationToken cancellationToken = default);
}
