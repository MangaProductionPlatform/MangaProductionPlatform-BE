using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MediatR;

namespace MangaERP.Identity.Application.Queries.GetUserStats;

// ── Query ─────────────────────────────────────────────────────────────────────

/// <summary>
/// Internal query — Identity module tự tổng hợp user stats của mình.
/// Được gọi bởi GetAdminDashboardHandler ở Api layer qua IMediator.Send().
/// KHÔNG expose IUserRepository ra ngoài module.
/// </summary>
public record GetUserStatsQuery(DateTime? StartDate = null, DateTime? EndDate = null) : IRequest<UserStatsResult>;

public record UserStatsResult(
    int TotalUsers,
    int ActiveUsers,
    int PendingActivation,
    int SuspendedUsers,
    int TotalMangaka,
    int TotalAssistants,
    int TotalTantouEditors,
    int TotalEditorialBoard,
    int TotalEditorInChief,
    int TotalAdmins
);

// ── Handler ───────────────────────────────────────────────────────────────────

public class GetUserStatsHandler : IRequestHandler<GetUserStatsQuery, UserStatsResult>
{
    private readonly IUserRepository _repo;

    public GetUserStatsHandler(IUserRepository repo) => _repo = repo;

    public async Task<UserStatsResult> Handle(GetUserStatsQuery request, CancellationToken ct)
    {
        var usersQuery = (await _repo.GetAllAsync(ct)).AsQueryable();

        if (request.StartDate.HasValue)
            usersQuery = usersQuery.Where(u => u.CreatedAt >= request.StartDate.Value.Date);
        if (request.EndDate.HasValue)
        {
            var nextDay = request.EndDate.Value.Date.AddDays(1);
            usersQuery = usersQuery.Where(u => u.CreatedAt < nextDay);
        }

        var users = usersQuery.ToList();

        return new UserStatsResult(
            TotalUsers:          users.Count,
            ActiveUsers:         users.Count(u => u.AccountStatus == AccountStatus.Active),
            PendingActivation:   users.Count(u => u.AccountStatus == AccountStatus.PendingActivation),
            SuspendedUsers:      users.Count(u => u.AccountStatus == AccountStatus.Suspended),
            TotalMangaka:        users.Count(u => u.Role == UserRole.Mangaka),
            TotalAssistants:     users.Count(u => u.Role == UserRole.Assistant),
            TotalTantouEditors:  users.Count(u => u.Role == UserRole.TantouEditor),
            TotalEditorialBoard: users.Count(u => u.Role == UserRole.EditorialBoard),
            TotalEditorInChief:  users.Count(u => u.Role == UserRole.EditorInChief),
            TotalAdmins:         users.Count(u => u.Role == UserRole.Admin)
        );
    }
}
