using MangaERP.Submission.Domain.Entities;

namespace MangaERP.Submission.Application.Ports;

public interface ISubmissionRepository
{
    Task<SeriesSubmission?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IEnumerable<SeriesSubmission>> GetBySubmitterAsync(Guid submitterId, CancellationToken cancellationToken = default);
    Task<IEnumerable<SeriesSubmission>> GetVettingQueueAsync(CancellationToken cancellationToken = default);
    Task AddAsync(SeriesSubmission submission, CancellationToken cancellationToken = default);
    Task UpdateAsync(SeriesSubmission submission, CancellationToken cancellationToken = default);
}
