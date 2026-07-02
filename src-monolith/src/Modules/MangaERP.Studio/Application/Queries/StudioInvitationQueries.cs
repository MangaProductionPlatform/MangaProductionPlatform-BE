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

// ── Query: Mangaka xem danh sách thành viên đang hoạt động trong Studio ──────

public record GetStudioMembersQuery(Guid RequesterId, Guid SeriesId)
    : IRequest<IEnumerable<StudioMemberDto>>;

public class GetStudioMembersHandler
    : IRequestHandler<GetStudioMembersQuery, IEnumerable<StudioMemberDto>>
{
    private readonly IStudioInvitationRepository _repo;
    private readonly MangaERP.Series.Application.Ports.ISeriesRepository _seriesRepo;

    public GetStudioMembersHandler(
        IStudioInvitationRepository repo,
        MangaERP.Series.Application.Ports.ISeriesRepository seriesRepo)
    {
        _repo = repo;
        _seriesRepo = seriesRepo;
    }

    public async Task<IEnumerable<StudioMemberDto>> Handle(GetStudioMembersQuery request, CancellationToken ct)
    {
        var series = await _seriesRepo.GetByIdAsync(request.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {request.SeriesId} not found.");

        // Chỉ Mangaka sở hữu series mới có thể xem danh sách thành viên studio
        if (series.AuthorId != request.RequesterId)
            throw new UnauthorizedAccessException("You do not have access to this studio's member list.");

        var members = await _repo.GetActiveMembersWithUsersBySeriesIdAsync(request.SeriesId, ct);

        return members.Select(m => new StudioMemberDto(
            m.InvitationId,
            m.AssistantUserId,
            m.AssistantEmail,
            m.FullName,
            m.AvatarUrl,
            m.PenName,
            m.InvitationStatus,
            m.JoinedAt
        ));
    }
}

// ── DTOs ─────────────────────────────────────────────────────────────────────

public record StudioInvitationDto(
    Guid InvitationId,
    Guid SeriesId,
    Guid InviterMangakaId,
    string AssistantEmail,
    string? Message,
    string Status,
    DateTime ExpiresAt
);

public record StudioMemberDto(
    Guid InvitationId,
    Guid AssistantUserId,
    string AssistantEmail,
    string? FullName,
    string? AvatarUrl,
    string? PenName,
    string Status,
    DateTime JoinedAt
);
