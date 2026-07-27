using MediatR;
using MangaERP.Studio.Application.Ports;

namespace MangaERP.Studio.Application.Queries.GetUnassignedAssistants;

public record GetUnassignedAssistantsQuery(Guid MangakaId) : IRequest<UnassignedAssistantsResponseDto>;

public record UnassignedAssistantsResponseDto(
    List<UnassignedAssistantItemDto> UnassignedAssistants
);

public record UnassignedAssistantItemDto(
    Guid UserId,
    string Username,
    string FullName,
    string PersonalEmail,
    string? PhoneNumber,
    DateTime CreatedAt
);

public class GetUnassignedAssistantsHandler : IRequestHandler<GetUnassignedAssistantsQuery, UnassignedAssistantsResponseDto>
{
    private readonly IStudioInvitationRepository _collabRepo;

    public GetUnassignedAssistantsHandler(IStudioInvitationRepository collabRepo)
    {
        _collabRepo = collabRepo;
    }

    public async Task<UnassignedAssistantsResponseDto> Handle(GetUnassignedAssistantsQuery request, CancellationToken cancellationToken)
    {
        var items = await _collabRepo.GetUnassignedAssistantsAsync(request.MangakaId, cancellationToken);

        var list = items.Select(u => new UnassignedAssistantItemDto(
            u.UserId,
            u.Username,
            u.FullName,
            u.PersonalEmail,
            u.PhoneNumber,
            u.CreatedAt
        )).ToList();

        return new UnassignedAssistantsResponseDto(list);
    }
}
