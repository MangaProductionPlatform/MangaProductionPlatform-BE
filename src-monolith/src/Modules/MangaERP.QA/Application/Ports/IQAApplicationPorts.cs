using MangaERP.QA.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading;

namespace MangaERP.QA.Application.Ports;

public interface IBugPinRepository
{
    System.Threading.Tasks.Task<BugPin?> GetByIdAsync(Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<BugPin>> GetByChapterIdAsync(Guid chapterId, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(BugPin bugPin, CancellationToken ct = default);
    System.Threading.Tasks.Task UpdateAsync(BugPin bugPin, CancellationToken ct = default);
    System.Threading.Tasks.Task DeleteAsync(BugPin bugPin, CancellationToken ct = default);
}

public interface IQASessionRepository
{
    System.Threading.Tasks.Task<QASession?> GetByIdAsync(Guid id, CancellationToken ct = default);
    System.Threading.Tasks.Task<QASession?> GetByChapterIdAsync(Guid chapterId, CancellationToken ct = default);
    System.Threading.Tasks.Task<IEnumerable<QASession>> GetAllByChapterIdAsync(Guid chapterId, CancellationToken ct = default);
    System.Threading.Tasks.Task AddAsync(QASession session, CancellationToken ct = default);
    System.Threading.Tasks.Task UpdateAsync(QASession session, CancellationToken ct = default);
}
