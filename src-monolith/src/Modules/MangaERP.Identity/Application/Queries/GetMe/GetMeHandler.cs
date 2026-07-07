using MangaERP.Identity.Application.Ports;
using MangaERP.Shared.Domain.Exceptions;
using MediatR;

namespace MangaERP.Identity.Application.Queries.GetMe;

// ── Query & Result ─────────────────────────────────────────────────────────────
public record GetMeQuery(Guid UserId) : IRequest<GetMeResult>;

public record GetMeResult(
    Guid   UserId,
    string Username,
    string Email,
    string Role,
    string AccountStatus,
    string? FullName,
    string? AvatarUrl,
    string? PenName,
    string? PhoneNumber,
    string[]? DrawingSoftwares,
    string? BankAccountNumber,
    Guid?  ManagingTantouId,
    DateTime CreatedAt
);

// ── Handler ───────────────────────────────────────────────────────────────────
public class GetMeHandler : IRequestHandler<GetMeQuery, GetMeResult>
{
    private readonly IUserRepository _userRepo;

    public GetMeHandler(IUserRepository userRepo) => _userRepo = userRepo;

    public async Task<GetMeResult> Handle(GetMeQuery request, CancellationToken ct)
    {
        var user = await _userRepo.GetByIdAsync(request.UserId, ct)
            ?? throw new EntityNotFoundException("User", request.UserId);

        var softwares = string.IsNullOrWhiteSpace(user.DrawingSoftwares)
            ? null
            : user.DrawingSoftwares.Split(',', StringSplitOptions.RemoveEmptyEntries);

        return new GetMeResult(
            UserId:           user.Id,
            Username:         user.Username,
            Email:            user.Email,
            Role:             user.Role.ToString(),
            AccountStatus:    user.AccountStatus.ToString(),
            FullName:         user.FullName,
            AvatarUrl:        user.AvatarUrl,
            PenName:          user.PenName,
            PhoneNumber:      user.PhoneNumber,
            DrawingSoftwares: softwares,
            BankAccountNumber: user.BankAccountNumber,
            ManagingTantouId: user.ManagingTantouId,
            CreatedAt:        user.CreatedAt
        );
    }
}
