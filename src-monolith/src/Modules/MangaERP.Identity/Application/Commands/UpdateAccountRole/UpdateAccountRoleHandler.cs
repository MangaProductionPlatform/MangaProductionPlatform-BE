using MediatR;
using MangaERP.Identity.Application.Ports;
using MangaERP.Identity.Domain.Enums;
using MangaERP.Shared.Domain.Exceptions;

namespace MangaERP.Identity.Application.Commands.UpdateAccountRole;

public record UpdateAccountRoleCommand(
    Guid UserId,
    UserRole Role
) : IRequest<UpdateAccountRoleResult>;

public record UpdateAccountRoleResult(
    Guid UserId,
    string Username,
    string Role
);

public class UpdateAccountRoleHandler : IRequestHandler<UpdateAccountRoleCommand, UpdateAccountRoleResult>
{
    private readonly IUserRepository _userRepo;
    private readonly IUsernameGenerator _usernameGenerator;

    public UpdateAccountRoleHandler(
        IUserRepository userRepo,
        IUsernameGenerator usernameGenerator)
    {
        _userRepo = userRepo;
        _usernameGenerator = usernameGenerator;
    }

    public async Task<UpdateAccountRoleResult> Handle(UpdateAccountRoleCommand request, CancellationToken cancellationToken)
    {
        if (request.Role == UserRole.Admin || request.Role == UserRole.Reader)
        {
            throw new InvalidOperationException("Không thể gán vai trò Admin hoặc Reader cho tài khoản.");
        }

        var user = await _userRepo.GetByIdAsync(request.UserId, cancellationToken)
            ?? throw new EntityNotFoundException("User", request.UserId);

        if (user.Role != request.Role)
        {
            // If role changes, generate new corporate username
            var newUsername = await _usernameGenerator.GenerateAsync(user.FullName ?? "user", request.Role, cancellationToken);
            user.Username = newUsername;
            user.Email = newUsername;
            user.Role = request.Role;

            if (user.Role != UserRole.Mangaka)
            {
                user.ManagingTantouId = null;
            }
            
            await _userRepo.UpdateAsync(user, cancellationToken);
        }

        return new UpdateAccountRoleResult(user.Id, user.Username, user.Role.ToString());
    }
}
