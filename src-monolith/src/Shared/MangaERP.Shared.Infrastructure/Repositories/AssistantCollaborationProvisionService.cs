using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Infrastructure.Persistence;
using MangaERP.Studio.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using TaskAlias = System.Threading.Tasks.Task;

namespace MangaERP.Shared.Infrastructure.Repositories;

public class AssistantCollaborationProvisionService : IAssistantCollaborationProvisionPort
{
    private readonly AppDbContext _db;

    public AssistantCollaborationProvisionService(IDbContextProvider dbProvider)
    {
        _db = (AppDbContext)dbProvider.GetDbContext();
    }

    public System.Threading.Tasks.Task<bool> HasNonEndedCollaborationAsync(Guid assistantId, CancellationToken ct = default)
    {
        return _db.MangakaAssistantCollaborations.AnyAsync(
            c => c.AssistantId == assistantId && c.Status != CollaborationStatus.Ended, ct);
    }

    public async System.Threading.Tasks.Task CreateActiveCollaborationAsync(Guid mangakaId, Guid assistantId, CancellationToken ct = default)
    {
        var adminInvitationId = Guid.NewGuid();
        var collaboration = new MangakaAssistantCollaboration(mangakaId, assistantId, adminInvitationId, DateTime.UtcNow);
        await _db.MangakaAssistantCollaborations.AddAsync(collaboration, ct);

        var evt = new CollaborationEvent(
            collaboration.Id, CollaborationEventType.CollaborationActivated, assistantId, DateTime.UtcNow, "Admin Provisioning");
        await _db.CollaborationEvents.AddAsync(evt, ct);
    }
}
