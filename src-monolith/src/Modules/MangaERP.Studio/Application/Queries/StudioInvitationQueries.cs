using MediatR;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;

namespace MangaERP.Studio.Application.Queries;

// ── Query: Assistant xem lời mời đang chờ xử lý ─────────────────────────────

public record GetPendingInvitationsQuery(Guid AssistantUserId)
    : IRequest<IEnumerable<StudioInvitationDto>>;

public class GetPendingInvitationsHandler
    : IRequestHandler<GetPendingInvitationsQuery, IEnumerable<StudioInvitationDto>>
{
    private readonly IStudioInvitationRepository _repo;
    public GetPendingInvitationsHandler(IStudioInvitationRepository repo) => _repo = repo;

    public async Task<IEnumerable<StudioInvitationDto>> Handle(GetPendingInvitationsQuery request, CancellationToken ct)
    {
        var invitations = await _repo.GetPendingByAssistantUserIdAsync(request.AssistantUserId, ct);
        return invitations.Select(i => new StudioInvitationDto(
            i.Id,
            i.SeriesId,
            i.InviterMangakaId,
            i.AssistantEmail,
            i.Message,
            i.Status.ToString(),
            i.ExpiresAt
        ));
    }
}

// ── Query: Mangaka xem lịch sử mời của một series ────────────────────────────

public record GetSeriesInvitationsQuery(Guid MangakaId, Guid SeriesId)
    : IRequest<IEnumerable<StudioInvitationDto>>;

public class GetSeriesInvitationsHandler
    : IRequestHandler<GetSeriesInvitationsQuery, IEnumerable<StudioInvitationDto>>
{
    private readonly IStudioInvitationRepository _repo;
    public GetSeriesInvitationsHandler(IStudioInvitationRepository repo) => _repo = repo;

    public async Task<IEnumerable<StudioInvitationDto>> Handle(GetSeriesInvitationsQuery request, CancellationToken ct)
    {
        var invitations = await _repo.GetBySeriesIdAsync(request.SeriesId, ct);

        // Chỉ trả về invitation của series thuộc Mangaka này
        return invitations
            .Where(i => i.InviterMangakaId == request.MangakaId)
            .Select(i => new StudioInvitationDto(
                i.Id,
                i.SeriesId,
                i.InviterMangakaId,
                i.AssistantEmail,
                i.Message,
                i.Status.ToString(),
                i.ExpiresAt
            ));
    }
}

// ── DTO ───────────────────────────────────────────────────────────────────────

public record StudioInvitationDto(
    Guid InvitationId,
    Guid SeriesId,
    Guid InviterMangakaId,
    string AssistantEmail,
    string? Message,
    string Status,
    DateTime ExpiresAt
);
