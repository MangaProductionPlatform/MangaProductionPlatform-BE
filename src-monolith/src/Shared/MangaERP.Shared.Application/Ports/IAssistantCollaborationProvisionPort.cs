namespace MangaERP.Shared.Application.Ports;

public interface IAssistantCollaborationProvisionPort
{
    System.Threading.Tasks.Task<bool> HasNonEndedCollaborationAsync(Guid assistantId, CancellationToken ct = default);
    System.Threading.Tasks.Task CreateActiveCollaborationAsync(Guid mangakaId, Guid assistantId, CancellationToken ct = default);
}
