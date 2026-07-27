using MediatR;
using MangaERP.Studio.Application.Ports;

namespace MangaERP.Studio.Application.Commands.AssignAssistantToMangaka;

public record AssignAssistantToMangakaCommand(
    Guid AssistantId,
    Guid MangakaId,
    Guid AdminUserId,
    string? Reason
) : IRequest<AssignAssistantToMangakaResult>;

public record AssignAssistantToMangakaResult(
    Guid CollaborationId,
    Guid AssistantId,
    Guid MangakaId,
    string Status,
    DateTime StartedAt
);

public class AssignAssistantToMangakaHandler : IRequestHandler<AssignAssistantToMangakaCommand, AssignAssistantToMangakaResult>
{
    private readonly IStudioInvitationRepository _collabRepo;

    public AssignAssistantToMangakaHandler(IStudioInvitationRepository collabRepo)
    {
        _collabRepo = collabRepo;
    }

    public async Task<AssignAssistantToMangakaResult> Handle(AssignAssistantToMangakaCommand request, CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;

        var collaboration = await _collabRepo.AdminAssignAssistantToMangakaAsync(
            request.AssistantId,
            request.MangakaId,
            request.AdminUserId,
            request.Reason,
            now,
            cancellationToken
        );

        return new AssignAssistantToMangakaResult(
            collaboration.Id,
            collaboration.AssistantId,
            collaboration.MangakaId,
            collaboration.Status.ToString(),
            collaboration.StartedAt
        );
    }
}
