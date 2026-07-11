using MangaERP.QA.Application.Ports;
using MangaERP.QA.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace MangaERP.Shared.Infrastructure.Repositories;

public class QARepositories : IBugPinRepository, IQASessionRepository
{
    private readonly AppDbContext _db;

    public QARepositories(IDbContextProvider provider)
    {
        _db = (AppDbContext)provider.GetDbContext();
    }

    // ── IBugPinRepository Implementation ──────────────────────────

    public async System.Threading.Tasks.Task<BugPin?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.BugPins.FirstOrDefaultAsync(b => b.Id == id, ct);

    public async System.Threading.Tasks.Task<IEnumerable<BugPin>> GetByChapterIdAsync(Guid chapterId, CancellationToken ct = default)
        => await _db.BugPins
            .Where(b => b.ChapterId == chapterId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<IEnumerable<BugPin>> GetByPageTaskIdAsync(Guid pageTaskId, CancellationToken ct = default)
        => await _db.BugPins
            .Where(b => b.PageTaskId == pageTaskId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task AddAsync(BugPin bugPin, CancellationToken ct = default)
    {
        await _db.BugPins.AddAsync(bugPin, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task UpdateAsync(BugPin bugPin, CancellationToken ct = default)
    {
        _db.BugPins.Update(bugPin);
        await _db.SaveChangesAsync(ct);
    }

    public async System.Threading.Tasks.Task DeleteAsync(BugPin bugPin, CancellationToken ct = default)
    {
        _db.BugPins.Remove(bugPin);
        await _db.SaveChangesAsync(ct);
    }

    // ── IQASessionRepository Implementation ───────────────────────

    async System.Threading.Tasks.Task<QASession?> IQASessionRepository.GetByIdAsync(Guid id, CancellationToken ct)
        => await _db.QASessions.FirstOrDefaultAsync(q => q.Id == id, ct);

    async System.Threading.Tasks.Task<QASession?> IQASessionRepository.GetByChapterIdAsync(Guid chapterId, CancellationToken ct)
        => await _db.QASessions.FirstOrDefaultAsync(q => q.ChapterId == chapterId, ct);

    async System.Threading.Tasks.Task<IEnumerable<QASession>> IQASessionRepository.GetAllByChapterIdAsync(Guid chapterId, CancellationToken ct)
        => await _db.QASessions
            .Where(q => q.ChapterId == chapterId)
            .OrderByDescending(q => q.CreatedAt)
            .ToListAsync(ct);

    async System.Threading.Tasks.Task IQASessionRepository.AddAsync(QASession session, CancellationToken ct)
    {
        await _db.QASessions.AddAsync(session, ct);
        await _db.SaveChangesAsync(ct);
    }

    async System.Threading.Tasks.Task IQASessionRepository.UpdateAsync(QASession session, CancellationToken ct)
    {
        _db.QASessions.Update(session);
        await _db.SaveChangesAsync(ct);
    }
}
