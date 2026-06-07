using MangaERP.Task.Application.Ports;
using MangaERP.Task.Domain.Entities;
using MangaERP.Task.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Task.Infrastructure.Repositories;

public class ArtworkLayerRepository : IArtworkLayerRepository
{
    private readonly TaskDbContext _context;

    public ArtworkLayerRepository(TaskDbContext context) => _context = context;

    public async Task<ArtworkLayer?> GetCurrentByPageTaskAsync(Guid pageTaskId, CancellationToken cancellationToken = default)
        => await _context.ArtworkLayers
            .Where(l => l.PageTaskId == pageTaskId && l.IsCurrentVersion)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IEnumerable<ArtworkLayer>> GetHistoryByPageTaskAsync(Guid pageTaskId, CancellationToken cancellationToken = default)
        => await _context.ArtworkLayers
            .Where(l => l.PageTaskId == pageTaskId)
            .OrderByDescending(l => l.Version)
            .ToListAsync(cancellationToken);

    public async Task<IEnumerable<ArtworkLayer>> GetByAssistantAsync(Guid assistantId, CancellationToken cancellationToken = default)
        => await _context.ArtworkLayers
            .Where(l => l.AssistantId == assistantId && l.IsCurrentVersion)
            .ToListAsync(cancellationToken);

    public async System.Threading.Tasks.Task AddAsync(ArtworkLayer layer, CancellationToken cancellationToken = default)
    {
        await _context.ArtworkLayers.AddAsync(layer, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async System.Threading.Tasks.Task UpdateAsync(ArtworkLayer layer, CancellationToken cancellationToken = default)
    {
        _context.ArtworkLayers.Update(layer);
        await _context.SaveChangesAsync(cancellationToken);
    }
}

public class AssistantInvitationRepository : IAssistantInvitationRepository
{
    private readonly TaskDbContext _context;

    public AssistantInvitationRepository(TaskDbContext context) => _context = context;

    public async System.Threading.Tasks.Task<AssistantInvitation?> GetByInvitationTokenAsync(Guid invitationToken, CancellationToken cancellationToken = default)
        => await _context.AssistantInvitations
            .FirstOrDefaultAsync(i => i.InvitationToken == invitationToken, cancellationToken);

    public async System.Threading.Tasks.Task AddAsync(AssistantInvitation invitation, CancellationToken cancellationToken = default)
    {
        await _context.AssistantInvitations.AddAsync(invitation, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async System.Threading.Tasks.Task UpdateAsync(AssistantInvitation invitation, CancellationToken cancellationToken = default)
    {
        _context.AssistantInvitations.Update(invitation);
        await _context.SaveChangesAsync(cancellationToken);
    }
}
