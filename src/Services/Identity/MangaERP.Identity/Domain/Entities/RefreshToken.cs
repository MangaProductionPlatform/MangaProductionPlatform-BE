namespace MangaERP.Identity.Domain.Entities;

/// <summary>
/// Refresh token entity for JWT lifecycle management.
/// [NEW table from DB v2] — invalidated on logout and role elevation.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public bool IsRevoked { get; set; } = false;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RevokedAt { get; set; }

    // Navigation
    public virtual User User { get; set; } = null!;
}
