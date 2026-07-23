using MangaERP.Shared.Domain.Abstractions;
using MangaERP.Shared.Domain.Exceptions;

namespace MangaERP.Studio.Domain.Entities;

public class SeriesAccessGrant : AggregateRoot
{
    public Guid CollaborationId { get; private set; }
    public Guid SeriesId { get; private set; }
    public Guid GrantedByUserId { get; private set; }
    public DateTime GrantedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; private set; }
    public Guid? RevokedByUserId { get; private set; }
    public string? RevokeReason { get; private set; }
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; private set; } = DateTime.UtcNow;
    public Guid ConcurrencyToken { get; private set; } = Guid.NewGuid();

    public bool IsActive => RevokedAt == null;

    private SeriesAccessGrant() { }

    public static SeriesAccessGrant Create(Guid collaborationId, Guid seriesId, Guid grantedByUserId)
    {
        if (collaborationId == Guid.Empty) throw new ArgumentException("CollaborationId is required.", nameof(collaborationId));
        if (seriesId == Guid.Empty) throw new ArgumentException("SeriesId is required.", nameof(seriesId));
        if (grantedByUserId == Guid.Empty) throw new ArgumentException("GrantedByUserId is required.", nameof(grantedByUserId));

        return new SeriesAccessGrant
        {
            Id = Guid.NewGuid(),
            CollaborationId = collaborationId,
            SeriesId = seriesId,
            GrantedByUserId = grantedByUserId,
            GrantedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ConcurrencyToken = Guid.NewGuid()
        };
    }

    public void Revoke(Guid revokedByUserId, string? reason)
    {
        if (!IsActive)
            throw new ConflictException("Series access grant is already revoked.");

        if (revokedByUserId == Guid.Empty)
            throw new ArgumentException("RevokedByUserId is required.", nameof(revokedByUserId));

        RevokedAt = DateTime.UtcNow;
        RevokedByUserId = revokedByUserId;
        RevokeReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        UpdatedAt = DateTime.UtcNow;
        ConcurrencyToken = Guid.NewGuid();
    }
}
