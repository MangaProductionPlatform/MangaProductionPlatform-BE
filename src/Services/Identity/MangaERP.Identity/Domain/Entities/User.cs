using MangaERP.BuildingBlocks.Infrastructure.Persistence;
using MangaERP.Identity.Domain.Enums;

namespace MangaERP.Identity.Domain.Entities;

/// <summary>
/// User entity. Soft-deletable. Includes FIX-1 fields: FullName and AvatarUrl.
/// </summary>
public class User : ISoftDeletable
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Reader;

    // [FIX-1] Profile fields
    public string? FullName { get; set; }
    public string? AvatarUrl { get; set; }

    // Soft Delete
    public bool IsDeleted { get; set; } = false;
    public DateTime? DeletedAt { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public virtual ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();
}
