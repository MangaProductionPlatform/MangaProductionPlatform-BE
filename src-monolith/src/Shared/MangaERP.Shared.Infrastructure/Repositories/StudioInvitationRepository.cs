using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Shared.Infrastructure.Repositories;

public class StudioInvitationRepository : IStudioInvitationRepository
{
    private readonly AppDbContext _db;

    public StudioInvitationRepository(IDbContextProvider provider)
        => _db = (AppDbContext)provider.GetDbContext();

    public async System.Threading.Tasks.Task<StudioInvitation?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await _db.StudioInvitations.FirstOrDefaultAsync(i => i.Id == id, ct);

    public async System.Threading.Tasks.Task<IEnumerable<StudioInvitation>> GetPendingByAssistantUserIdAsync(Guid assistantUserId, CancellationToken ct = default)
        => await _db.StudioInvitations
            .Where(i => i.AssistantUserId == assistantUserId && i.Status == StudioInvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<IEnumerable<StudioInvitation>> GetBySeriesIdAsync(Guid seriesId, CancellationToken ct = default)
        => await _db.StudioInvitations
            .Where(i => i.SeriesId == seriesId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(ct);

    public async System.Threading.Tasks.Task<StudioInvitation?> GetByActivationTokenAsync(string token, CancellationToken ct = default)
        => await _db.StudioInvitations.FirstOrDefaultAsync(i => i.ActivationToken == token, ct);

    public async System.Threading.Tasks.Task AddAsync(StudioInvitation invitation, CancellationToken ct = default)
        => await _db.StudioInvitations.AddAsync(invitation, ct);

    public System.Threading.Tasks.Task UpdateAsync(StudioInvitation invitation, CancellationToken ct = default)
    {
        _db.StudioInvitations.Update(invitation);
        return System.Threading.Tasks.Task.CompletedTask;
    }

    public System.Threading.Tasks.Task<int> SaveChangesAsync(CancellationToken ct = default)
        => _db.SaveChangesAsync(ct);
}
