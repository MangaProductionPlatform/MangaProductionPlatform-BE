using MangaERP.Task.Domain.Entities;
using MangaERP.Task.Domain.Enums;

namespace MangaERP.Task.Application.Ports;

public interface IPageTaskWriteRepository
{
    /// <summary>Update PageTask status — used when an artwork layer is accepted/rejected.</summary>
    System.Threading.Tasks.Task<PageTaskProxy?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    System.Threading.Tasks.Task UpdateStatusAsync(Guid pageTaskId, string newStatus, CancellationToken cancellationToken = default);
}

/// <summary>
/// Lightweight read-model of a PageTask maintained by this service to avoid cross-service DB calls.
/// </summary>
public record PageTaskProxy(Guid Id, Guid ChapterId, Guid? AssignedAssistantId, string Status);

public interface IArtworkLayerRepository
{
    System.Threading.Tasks.Task<ArtworkLayer?> GetCurrentByPageTaskAsync(Guid pageTaskId, CancellationToken cancellationToken = default);
    System.Threading.Tasks.Task<IEnumerable<ArtworkLayer>> GetHistoryByPageTaskAsync(Guid pageTaskId, CancellationToken cancellationToken = default);
    System.Threading.Tasks.Task<IEnumerable<ArtworkLayer>> GetByAssistantAsync(Guid assistantId, CancellationToken cancellationToken = default);
    System.Threading.Tasks.Task AddAsync(ArtworkLayer layer, CancellationToken cancellationToken = default);
    System.Threading.Tasks.Task UpdateAsync(ArtworkLayer layer, CancellationToken cancellationToken = default);
}

public interface IAssistantInvitationRepository
{
    System.Threading.Tasks.Task<AssistantInvitation?> GetByInvitationTokenAsync(Guid invitationToken, CancellationToken cancellationToken = default);
    System.Threading.Tasks.Task AddAsync(AssistantInvitation invitation, CancellationToken cancellationToken = default);
    System.Threading.Tasks.Task UpdateAsync(AssistantInvitation invitation, CancellationToken cancellationToken = default);
}
