using MediatR;
using MangaERP.Shared.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MangaERP.Series.Application.Ports;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;

namespace MangaERP.Studio.Application.Commands.SeriesAccess;

public record GrantSeriesAccessCommand(Guid CollaborationId, Guid SeriesId, Guid ActorUserId) : IRequest<SeriesAccessGrantDto>;
public record RevokeSeriesAccessCommand(Guid CollaborationId, Guid SeriesId, Guid ActorUserId, string? Reason) : IRequest<Unit>;
public record GetCollaborationSeriesGrantsQuery(Guid CollaborationId, Guid ActorUserId) : IRequest<IEnumerable<SeriesAccessGrantDto>>;

public record SeriesAccessGrantDto(
    Guid Id,
    Guid CollaborationId,
    Guid SeriesId,
    Guid GrantedByUserId,
    DateTime GrantedAt,
    bool IsActive,
    DateTime? RevokedAt,
    Guid? RevokedByUserId,
    string? RevokeReason);

public sealed class GrantSeriesAccessHandler : IRequestHandler<GrantSeriesAccessCommand, SeriesAccessGrantDto>
{
    private readonly IStudioInvitationRepository _collabRepo;
    private readonly ISeriesAccessGrantRepository _grantRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly INotificationService _notifications;

    public GrantSeriesAccessHandler(
        IStudioInvitationRepository collabRepo,
        ISeriesAccessGrantRepository grantRepo,
        ISeriesRepository seriesRepo,
        INotificationService notifications)
    {
        _collabRepo = collabRepo;
        _grantRepo = grantRepo;
        _seriesRepo = seriesRepo;
        _notifications = notifications;
    }

    public async Task<SeriesAccessGrantDto> Handle(GrantSeriesAccessCommand request, CancellationToken ct)
    {
        var collaboration = await _collabRepo.GetCollaborationAsync(request.CollaborationId, ct)
            ?? throw new EntityNotFoundException("Collaboration", request.CollaborationId);

        if (collaboration.MangakaId != request.ActorUserId)
            throw new UnauthorizedAccessException("Only the Mangaka who owns the collaboration can grant series access.");

        if (collaboration.Status != CollaborationStatus.Active)
            throw new ConflictException($"Cannot grant series access to a collaboration in status '{collaboration.Status}'.");

        var series = await _seriesRepo.GetByIdAsync(request.SeriesId, ct)
            ?? throw new EntityNotFoundException("MangaSeries", request.SeriesId);

        if (series.AuthorId != request.ActorUserId)
            throw new UnauthorizedAccessException("You do not own this series.");

        var existingGrant = await _grantRepo.GetActiveGrantAsync(request.CollaborationId, request.SeriesId, ct);
        if (existingGrant != null)
            throw new ConflictException("An active series access grant already exists for this collaboration and series.");

        var grant = SeriesAccessGrant.Create(request.CollaborationId, request.SeriesId, request.ActorUserId);
        await _grantRepo.AddAsync(grant, ct);
        await _grantRepo.SaveChangesAsync(ct);

        await _notifications.NotifyCollaborationEventAsync(
            collaboration.AssistantId,
            "SeriesAccessGranted",
            "Series Access Granted",
            $"You have been granted access to series '{series.Title}'.",
            series.Id,
            ct);

        return new SeriesAccessGrantDto(
            grant.Id,
            grant.CollaborationId,
            grant.SeriesId,
            grant.GrantedByUserId,
            grant.GrantedAt,
            grant.IsActive,
            grant.RevokedAt,
            grant.RevokedByUserId,
            grant.RevokeReason);
    }
}

public sealed class RevokeSeriesAccessHandler : IRequestHandler<RevokeSeriesAccessCommand, Unit>
{
    private readonly IStudioInvitationRepository _collabRepo;
    private readonly ISeriesAccessGrantRepository _grantRepo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly INotificationService _notifications;

    public RevokeSeriesAccessHandler(
        IStudioInvitationRepository collabRepo,
        ISeriesAccessGrantRepository grantRepo,
        ISeriesRepository seriesRepo,
        INotificationService notifications)
    {
        _collabRepo = collabRepo;
        _grantRepo = grantRepo;
        _seriesRepo = seriesRepo;
        _notifications = notifications;
    }

    public async Task<Unit> Handle(RevokeSeriesAccessCommand request, CancellationToken ct)
    {
        var collaboration = await _collabRepo.GetCollaborationAsync(request.CollaborationId, ct)
            ?? throw new EntityNotFoundException("Collaboration", request.CollaborationId);

        if (collaboration.MangakaId != request.ActorUserId)
            throw new UnauthorizedAccessException("Only the Mangaka who owns the collaboration can revoke series access.");

        var series = await _seriesRepo.GetByIdAsync(request.SeriesId, ct)
            ?? throw new EntityNotFoundException("MangaSeries", request.SeriesId);

        if (series.AuthorId != request.ActorUserId)
            throw new UnauthorizedAccessException("You do not own this series.");

        var grant = await _grantRepo.GetActiveGrantAsync(request.CollaborationId, request.SeriesId, ct)
            ?? throw new ConflictException("No active series access grant exists to revoke.");

        grant.Revoke(request.ActorUserId, request.Reason);
        await _grantRepo.UpdateAsync(grant, ct);
        await _grantRepo.SaveChangesAsync(ct);

        await _notifications.NotifyCollaborationEventAsync(
            collaboration.AssistantId,
            "SeriesAccessRevoked",
            "Series Access Revoked",
            $"Your access to series '{series.Title}' has been revoked.",
            series.Id,
            ct);

        return Unit.Value;
    }
}

public sealed class GetCollaborationSeriesGrantsHandler : IRequestHandler<GetCollaborationSeriesGrantsQuery, IEnumerable<SeriesAccessGrantDto>>
{
    private readonly IStudioInvitationRepository _collabRepo;
    private readonly ISeriesAccessGrantRepository _grantRepo;

    public GetCollaborationSeriesGrantsHandler(
        IStudioInvitationRepository collabRepo,
        ISeriesAccessGrantRepository grantRepo)
    {
        _collabRepo = collabRepo;
        _grantRepo = grantRepo;
    }

    public async Task<IEnumerable<SeriesAccessGrantDto>> Handle(GetCollaborationSeriesGrantsQuery request, CancellationToken ct)
    {
        var collaboration = await _collabRepo.GetCollaborationAsync(request.CollaborationId, ct)
            ?? throw new EntityNotFoundException("Collaboration", request.CollaborationId);

        if (collaboration.MangakaId != request.ActorUserId && collaboration.AssistantId != request.ActorUserId)
            throw new UnauthorizedAccessException("You are not a member of this collaboration.");

        var grants = await _grantRepo.GetByCollaborationIdAsync(request.CollaborationId, ct);
        return grants.Select(g => new SeriesAccessGrantDto(
            g.Id,
            g.CollaborationId,
            g.SeriesId,
            g.GrantedByUserId,
            g.GrantedAt,
            g.IsActive,
            g.RevokedAt,
            g.RevokedByUserId,
            g.RevokeReason));
    }
}
