using MediatR;
using MangaERP.Studio.Application.Ports;

namespace MangaERP.Studio.Application.Queries.GetAdminUnassignedAssistants;

public record GetAdminUnassignedAssistantsQuery : IRequest<AdminUnassignedAssistantsResponseDto>;

public record AdminUnassignedAssistantsResponseDto(
    List<AdminUnassignedAssistantItemDto> Assistants
);

public record AdminUnassignedAssistantItemDto(
    Guid AssistantId,
    string DisplayName,
    string Email,
    string AccountStatus,
    DateTime? LastCollaborationEndedAt,
    Guid? PreviousMangakaId,
    string? PreviousMangakaName,
    bool IsAssignable
);

public class GetAdminUnassignedAssistantsHandler : IRequestHandler<GetAdminUnassignedAssistantsQuery, AdminUnassignedAssistantsResponseDto>
{
    private readonly IStudioInvitationRepository _collabRepo;

    public GetAdminUnassignedAssistantsHandler(IStudioInvitationRepository collabRepo)
    {
        _collabRepo = collabRepo;
    }

    public async Task<AdminUnassignedAssistantsResponseDto> Handle(GetAdminUnassignedAssistantsQuery request, CancellationToken cancellationToken)
    {
        var items = await _collabRepo.GetAdminUnassignedAssistantsAsync(cancellationToken);

        var list = items.Select(u => new AdminUnassignedAssistantItemDto(
            u.AssistantId,
            u.DisplayName,
            u.Email,
            u.AccountStatus,
            u.LastCollaborationEndedAt,
            u.PreviousMangakaId,
            u.PreviousMangakaName,
            u.IsAssignable
        )).ToList();

        return new AdminUnassignedAssistantsResponseDto(list);
    }
}
