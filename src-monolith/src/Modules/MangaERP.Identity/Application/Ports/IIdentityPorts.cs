using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;

namespace MangaERP.Identity.Application.Ports;

public interface IUserRepository
{
    Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default);
    Task<bool> PersonalEmailExistsActiveOrPendingAsync(string personalEmail, CancellationToken ct = default);
    Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default);
    Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default);
    Task<IEnumerable<User>> GetByRoleAsync(UserRole role, CancellationToken ct = default);
    Task AddAsync(User user, CancellationToken ct = default);
    Task UpdateAsync(User user, CancellationToken ct = default);
    Task DeleteAsync(User user, CancellationToken ct = default);
    Task<System.Collections.Generic.Dictionary<Guid, int>> GetTantouEditorsLoadAsync(System.Collections.Generic.List<Guid> teIds, CancellationToken ct = default);

    // ── RBAC role checking ────────────────────────────────────────────────────

    /// <summary>Returns true if the user has a specific RBAC role name (e.g. "EDITORIAL_BOARD").</summary>
    Task<bool> HasRbacRoleAsync(Guid userId, string roleName, CancellationToken ct = default);

    /// <summary>Returns all RBAC role names assigned to a user.</summary>
    Task<IEnumerable<string>> GetUserRoleNamesAsync(Guid userId, CancellationToken ct = default);
}

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task AddAsync(RefreshToken token, CancellationToken ct = default);
    Task UpdateAsync(RefreshToken token, CancellationToken ct = default);
    Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default);
}

public interface IInvitationTokenRepository
{
    Task<InvitationToken?> GetByTokenStringAsync(string token, CancellationToken ct = default);
    Task AddAsync(InvitationToken token, CancellationToken ct = default);
    Task UpdateAsync(InvitationToken token, CancellationToken ct = default);
}

public interface ITokenService
{
    string GenerateAccessToken(User user);
    RefreshToken GenerateRefreshToken(Guid userId);
    string GenerateInvitationToken(Guid userId, string personalEmail, string username, string role);
    (bool isValid, Guid userId, string personalEmail, string username, string role) ValidateInvitationToken(string token);
}

public interface IEmailService
{
    Task SendInvitationEmailAsync(string toEmail, string activationLink,
        string username, string fullName, CancellationToken ct = default);

    Task SendUsernameUpdatedEmailAsync(string toEmail, string newUsername,
        string fullName, CancellationToken ct = default);

    Task SendOtpEmailAsync(string toEmail, string otp, CancellationToken ct = default);
}

public interface IUsernameGenerator
{
    /// <summary>
    /// Generates a unique corporate username. Format: {firstName}{lastInitials}.{roleCode}@company.com
    /// Handles collisions by appending incremental integers.
    /// </summary>
    Task<string> GenerateAsync(string fullName, UserRole role, CancellationToken ct = default);
}
