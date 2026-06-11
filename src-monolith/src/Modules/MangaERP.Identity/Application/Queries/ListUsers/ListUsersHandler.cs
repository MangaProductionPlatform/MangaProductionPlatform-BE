using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Entities;
using MangaERP.Identity.Domain.Enums;

namespace MangaERP.Identity.Application.Queries.ListUsers;

public record ListUsersQuery(
    UserRole? RoleFilter = null,
    AccountStatus? StatusFilter = null
) : IRequest<ListUsersResult>;

public record UserSummaryDto(
    Guid UserId,
    string Username,
    string? FullName,
    string Role,
    string AccountStatus,
    string? PersonalEmail,
    DateTime CreatedAt
);

public record ListUsersResult(IEnumerable<UserSummaryDto> Users, int TotalCount);

public class ListUsersHandler : IRequestHandler<ListUsersQuery, ListUsersResult>
{
    private readonly IUserRepository _userRepo;
    public ListUsersHandler(IUserRepository userRepo) => _userRepo = userRepo;

    public async Task<ListUsersResult> Handle(ListUsersQuery request, CancellationToken cancellationToken)
    {
        IEnumerable<User> users;

        if (request.RoleFilter.HasValue)
            users = await _userRepo.GetByRoleAsync(request.RoleFilter.Value, cancellationToken);
        else
            users = await _userRepo.GetAllAsync(cancellationToken);

        // Apply status filter in memory (simple enough for admin use)
        if (request.StatusFilter.HasValue)
            users = users.Where(u => u.AccountStatus == request.StatusFilter.Value);

        var dtos = users.Select(u => new UserSummaryDto(
            u.Id, u.Username, u.FullName,
            u.Role.ToString(), u.AccountStatus.ToString(),
            u.PersonalEmail, u.CreatedAt
        )).ToList();

        return new ListUsersResult(dtos, dtos.Count);
    }
}
