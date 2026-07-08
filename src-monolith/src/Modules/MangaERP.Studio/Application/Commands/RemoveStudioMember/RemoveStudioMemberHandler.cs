using MediatR;
using MangaERP.Studio.Application.Ports;
using MangaERP.Studio.Domain.Entities;
using MangaERP.Series.Application.Ports;

namespace MangaERP.Studio.Application.Commands.RemoveStudioMember;

public record RemoveStudioMemberCommand(Guid RequesterId, Guid SeriesId, Guid AssistantId) : IRequest<Unit>;

public record StudioMemberRemovedNotification(Guid SeriesId, Guid AssistantId) : INotification;

public class RemoveStudioMemberHandler : IRequestHandler<RemoveStudioMemberCommand, Unit>
{
    private readonly IStudioInvitationRepository _repo;
    private readonly ISeriesRepository _seriesRepo;
    private readonly IMediator _mediator;

    public RemoveStudioMemberHandler(
        IStudioInvitationRepository repo,
        ISeriesRepository seriesRepo,
        IMediator mediator)
    {
        _repo = repo;
        _seriesRepo = seriesRepo;
        _mediator = mediator;
    }

    public async Task<Unit> Handle(RemoveStudioMemberCommand request, CancellationToken ct)
    {
        // 1. Verify ownership of the series
        var series = await _seriesRepo.GetByIdAsync(request.SeriesId, ct)
            ?? throw new KeyNotFoundException($"Series {request.SeriesId} not found.");

        if (series.AuthorId != request.RequesterId)
            throw new UnauthorizedAccessException("Only the series owner can remove studio members.");

        // 2. Find the active member invitation
        var invitations = await _repo.GetBySeriesIdAsync(request.SeriesId, ct);
        var activeInvitation = invitations.FirstOrDefault(i =>
            i.AssistantUserId == request.AssistantId &&
            i.Status == StudioInvitationStatus.Accepted);

        if (activeInvitation == null)
            throw new KeyNotFoundException($"Active studio membership for assistant {request.AssistantId} in series {request.SeriesId} not found.");

        // 3. Mark the invitation as Cancelled (removed)
        activeInvitation.Status = StudioInvitationStatus.Cancelled;
        activeInvitation.RespondedAt = DateTime.UtcNow;

        await _repo.UpdateAsync(activeInvitation, ct);
        await _repo.SaveChangesAsync(ct);

        // 4. Publish notification to revoke tasks in MangaERP.Chapter module
        await _mediator.Publish(new StudioMemberRemovedNotification(request.SeriesId, request.AssistantId), ct);

        return Unit.Value;
    }
}
