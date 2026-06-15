using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Application.Ports;
using Microsoft.EntityFrameworkCore;

namespace MangaERP.Identity.Infrastructure.Repositories;

public class UserRepository : IUserRepository
{
    private readonly DbContext _db;
    public UserRepository(IDbContextProvider provider) => _db = (DbContext)provider.GetDbContext();

    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => _db.Set<User>().FirstOrDefaultAsync(u => u.Id == id, ct);

    public Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
        => _db.Set<User>().FirstOrDefaultAsync(u => u.Email == email, ct);

    public Task<User?> GetByUsernameAsync(string username, CancellationToken ct = default)
        => _db.Set<User>().FirstOrDefaultAsync(u => u.Username == username, ct);

    public Task<bool> PersonalEmailExistsActiveOrPendingAsync(string personalEmail, CancellationToken ct = default)
        => _db.Set<User>().AnyAsync(u =>
            u.PersonalEmail == personalEmail &&
            (u.AccountStatus == AccountStatus.PendingActivation || u.AccountStatus == AccountStatus.Active), ct);

    public Task<bool> UsernameExistsAsync(string username, CancellationToken ct = default)
        => _db.Set<User>().AnyAsync(u => u.Username == username, ct);

    public async Task<IEnumerable<User>> GetAllAsync(CancellationToken ct = default)
        => await _db.Set<User>().ToListAsync(ct);

    public async Task<IEnumerable<User>> GetByRoleAsync(UserRole role, CancellationToken ct = default)
        => await _db.Set<User>().Where(u => u.Role == role).ToListAsync(ct);

    public async Task AddAsync(User user, CancellationToken ct = default)
    {
        await _db.Set<User>().AddAsync(user, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(User user, CancellationToken ct = default)
    {
        _db.Set<User>().Update(user);
        await _db.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(User user, CancellationToken ct = default)
    {
        _db.Set<User>().Remove(user);
        await _db.SaveChangesAsync(ct);
    }
}

public class RefreshTokenRepository : IRefreshTokenRepository
{
    private readonly DbContext _db;
    public RefreshTokenRepository(IDbContextProvider provider) => _db = (DbContext)provider.GetDbContext();

    public Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
        => _db.Set<RefreshToken>().FirstOrDefaultAsync(r => r.Token == token && !r.IsRevoked, ct);

    public async Task AddAsync(RefreshToken token, CancellationToken ct = default)
    {
        await _db.Set<RefreshToken>().AddAsync(token, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(RefreshToken token, CancellationToken ct = default)
    {
        _db.Set<RefreshToken>().Update(token);
        await _db.SaveChangesAsync(ct);
    }

    public async Task RevokeAllForUserAsync(Guid userId, CancellationToken ct = default)
    {
        var tokens = await _db.Set<RefreshToken>()
            .Where(r => r.UserId == userId && !r.IsRevoked).ToListAsync(ct);
        foreach (var t in tokens) { t.IsRevoked = true; t.RevokedAt = DateTime.UtcNow; }
        await _db.SaveChangesAsync(ct);
    }
}

public class InvitationTokenRepository : IInvitationTokenRepository
{
    private readonly DbContext _db;
    public InvitationTokenRepository(IDbContextProvider provider) => _db = (DbContext)provider.GetDbContext();

    public Task<InvitationToken?> GetByTokenStringAsync(string token, CancellationToken ct = default)
        => _db.Set<InvitationToken>().FirstOrDefaultAsync(i => i.Token == token, ct);

    public async Task AddAsync(InvitationToken token, CancellationToken ct = default)
    {
        await _db.Set<InvitationToken>().AddAsync(token, ct);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(InvitationToken token, CancellationToken ct = default)
    {
        _db.Set<InvitationToken>().Update(token);
        await _db.SaveChangesAsync(ct);
    }
}
